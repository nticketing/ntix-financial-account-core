# Visão Geral — ntix-financial-account-core

## O que é este projeto?

O `ntix-financial-account-core` é o serviço responsável pelo **core financeiro** da plataforma nticketing. Ele centraliza todas as operações de movimentação financeira das contas digitais dos clientes, funcionando como a **fonte de verdade** (source of truth) para lançamentos contábeis e saldos.

Trata-se de um componente crítico da infraestrutura financeira: qualquer crédito ou débito realizado na conta de um cliente passa obrigatoriamente por este serviço antes de se propagar para os demais domínios da plataforma.

---

## Contexto de Negócio

### Conta Digital e Lançamentos Contábeis

No modelo de negócio da nticketing, cada cliente possui uma **conta digital** associada à plataforma. Essa conta é o instrumento pelo qual valores são recebidos (créditos) e consumidos (débitos) dentro do ecossistema — seja para pagamento de pedidos, recebimento de reembolsos, liquidação de transações comerciais, entre outros casos de uso.

Um **lançamento contábil** é o registro formal de uma movimentação financeira nessa conta. Ele carrega informações como o valor transacionado, o tipo da operação (crédito ou débito), o momento da ocorrência e identificadores únicos que permitem rastrear a operação desde sua origem.

O **saldo** da conta não é armazenado de forma independente — ele é a **projeção materializada** da somatória de todos os lançamentos registrados. Isso significa que a integridade do saldo depende diretamente da integridade dos lançamentos: se um lançamento for perdido, duplicado ou alterado, o saldo refletirá uma realidade incorreta.

### Funcionalidades Contempladas

#### Transação Contábil de Crédito em Conta

A primeira funcionalidade entregue pelo serviço é o registro de crédito em conta. Do ponto de vista de negócio, ela responde à seguinte necessidade: quando um valor precisa ser adicionado à conta digital de um cliente, o sistema deve garantir que esse crédito seja registrado uma única vez, que o saldo seja atualizado de forma consistente, e que os demais sistemas da plataforma sejam notificados sobre o evento.

Dois cenários são tratados:

- **Novo lançamento:** a solicitação de crédito ainda não foi processada. O lançamento é registrado, o saldo é incrementado e uma notificação é enviada para os consumidores downstream.
- **Lançamento duplicado (idempotência):** a solicitação já foi processada anteriormente com a mesma chave de operação. Nenhuma nova movimentação contábil é realizada, evitando créditos duplicados na conta do cliente.

Essa garantia de idempotência é fundamental para o negócio: em ambientes distribuídos, re-tentativas de envio são comuns, e o sistema precisa tratar esse cenário de forma transparente sem impactar o saldo do cliente.

#### Publicação Confiável de Eventos de Lançamento (Outbox Transactional Pattern)

Todo lançamento registrado no core deve ser propagado para outros domínios da plataforma — como extrato, auditoria, conciliação financeira e analytics. No entanto, essa propagação não pode ocorrer de forma desacoplada da persistência: se um evento fosse publicado antes da confirmação da gravação no banco de dados, e a gravação falhasse, outros sistemas receberiam e processariam um lançamento que na prática nunca existiu.

Para resolver isso, o serviço adota o **Outbox Transactional Pattern**: o evento de lançamento é gravado na mesma transação de banco de dados que registra o lançamento em si. Um processo dedicado (outbox worker) consulta periodicamente esses eventos pendentes e os publica no Kafka de forma ordenada e garantida.

A ordem de publicação é garantida por conta: o sistema assegura que os eventos de uma mesma conta sejam publicados cronologicamente, preservando a sequência histórica dos lançamentos para cada cliente.

---

## Impacto da Tecnologia no Negócio

As decisões técnicas adotadas neste serviço não são escolhas arbitrárias de engenharia — cada uma delas existe para proteger e viabilizar comportamentos críticos do negócio.

### Imutabilidade garante confiança e auditabilidade

Os lançamentos registrados são imutáveis: uma vez persistidos, não podem ser alterados ou removidos. Isso é uma exigência direta do negócio financeiro — qualquer histórico de movimentação deve ser auditável, rastreável e incontestável. A capacidade de reconstruir o "caminho" que levou o saldo de uma conta ao estado atual é o que permite atender reguladores, resolver disputas e manter a transparência com o cliente.

### Consistência transacional preserva a integridade do saldo

O saldo materializado e o registro do lançamento são atualizados dentro da mesma transação de banco de dados. Isso elimina a possibilidade de um lançamento ser registrado sem que o saldo seja atualizado, ou vice-versa. Do ponto de vista do cliente, significa que o saldo exibido na plataforma sempre refletirá com precisão o total das suas movimentações — sem janelas de inconsistência.

### Idempotência protege o cliente de cobranças e créditos duplicados

A verificação de idempotência por chave de operação garante que, mesmo diante de falhas de rede, re-tentativas automáticas ou erros de integração entre sistemas, um lançamento nunca seja aplicado mais de uma vez na conta do cliente. Sem essa proteção, um único erro de comunicação poderia resultar em saldo duplicado ou cobranças indevidas.

### O Outbox Pattern assegura que nenhum evento seja perdido ou inventado

A separação entre "persistir" e "publicar" — com o Outbox Pattern garantindo que a publicação só ocorra após a confirmação da persistência — protege todos os domínios downstream. Serviços de extrato, conciliação e analytics constroem suas visões sobre os eventos publicados por este core. Se um evento fosse publicado para um lançamento que não chegou a ser persistido, toda a cadeia downstream estaria processando uma informação falsa, com consequências financeiras e de compliance potencialmente graves.

### Resiliência operacional sustenta a disponibilidade do serviço

Estratégias de retry e dead-letter queue (DLQ) para consumo de mensagens garantem que falhas temporárias de infraestrutura — como instabilidades no banco de dados ou no broker de mensagens — não resultem em perda silenciosa de lançamentos. Operações que não puderam ser processadas são preservadas para análise e reprocessamento, mantendo o SLA financeiro da plataforma mesmo sob condições adversas.

### Particionamento por conta garante ordenação e escalabilidade

O roteamento de mensagens utilizando o número da conta como chave de partição no Kafka garante que todos os lançamentos de uma mesma conta sejam processados em ordem, por um único consumidor, sem disputas de concorrência. Ao mesmo tempo, contas diferentes são processadas em paralelo, permitindo que o serviço escale horizontalmente sem comprometer a consistência por conta individual.

---

## Componentes do Serviço

O `ntix-financial-account-core` é composto por três processos principais que trabalham em conjunto:

**ntix-financial-account-core-api** — ponto de entrada HTTP que recebe as solicitações de lançamento, realiza validações de contrato (fast-fail) e publica a solicitação no tópico Kafka `ntix.financial.accounting.entries-requested`. Responde com `202 Accepted`, indicando que a solicitação foi aceita para processamento assíncrono.

**ntix-financial-account-core-worker** — consumidor Kafka que processa as solicitações de lançamento particionadas por conta. Valida a solicitação, verifica idempotência, e — quando aplicável — persiste o lançamento, atualiza o saldo materializado e grava a mensagem de Outbox, tudo dentro de uma única transação de banco de dados.

**ntix-financial-account-core-outbox-worker** — processo dedicado ao dequeue e publicação dos eventos de lançamento pendentes no tópico `ntix.financial.accounting.entries-facts`, garantindo a entrega ordenada por conta e marcando cada evento como processado após a publicação bem-sucedida.

---

*Documentação inicial gerada em 30/06/2026. Referências: [Issue #1](https://github.com/nticketing/ntix-financial-account-core/issues/1) · [Issue #2](https://github.com/nticketing/ntix-financial-account-core/issues/2) · [Issue #3](https://github.com/nticketing/ntix-financial-account-core/issues/3)*
