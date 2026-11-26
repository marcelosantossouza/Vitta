# Backlog do Vitta

Melhorias combinadas e ainda não feitas. Ordem dentro de cada bloco = prioridade sugerida.

## Prospecção

1. **Importação como lote (histórico + desfazer).** Hoje os prospects importados não guardam de qual arquivo vieram,
   então não há como apagar uma importação. Criar o registro da importação (arquivo, modelo, data, quem importou,
   criados/ignorados), listar o histórico e permitir **excluir a importação**, removendo os prospects criados por ela.
   Decidir: prospect que já foi trabalhado (mudou de etapa, ganhou anotação ou virou cliente) sai junto ou fica?
   Sugestão: fica, e a tela avisa quantos foram mantidos.
2. **Repetidos: pular ou atualizar.** Hoje o repetido (mesmo e-mail, mesmo telefone ou mesmo nome sem contato) é só
   ignorado; dados novos da planilha se perdem. Dar a opção, na importação, de **atualizar os campos vazios/antigos** do
   prospect existente, e mostrar a lista de quem foi ignorado ou atualizado.
3. **Mais informações por prospect.** As planilhas costumam trazer: cidade, UF, endereço, telefones, salário base
   estimado, descontos, empréstimos (margem), entre outros. Campos fixos para os comuns (cidade, UF, endereço,
   telefone 2, data de nascimento, salário, descontos, empréstimos) + **campos personalizados** para o resto (mesma
   ideia do catálogo de campos com tipo da carteira, abaixo).
4. **Tela de gestão em lista** (além do quadro por etapa): filtros por cidade, UF, faixa de salário, etapa, origem,
   próxima data de contato e importação; ordenação; seleção em lote (mudar etapa, agendar contato); exportar o filtro.
5. **Dashboard da prospecção:** funil por etapa, taxa de conversão, prospects por origem/cidade/importação, contatos
   atrasados, novos e convertidos por semana.
6. **Cuidado LGPD:** salário, descontos e empréstimos são dados pessoais financeiros. Registrar a origem de cada lista
   (base legal) e limitar quem vê esses campos (ex.: perfil Assistente sem acesso a dados financeiros).

## Carteira

1. **Campos personalizados** (catálogo de campos com nome, tipo e descrição + valores em JSONB), para que colunas
   específicas de cada seguradora apareçam na tela, com filtro e ordenação, em vez de irem para "Outras informações".
2. Saídas da carteira encerrarem automaticamente a apólice cadastrada (com o motivo), em vez de só aparecerem na lista.

## Usuários e acesso

1. **"Esqueci minha senha"** por e-mail (link com validade curta). O envio de e-mail já existe.
2. **Multi-corretora:** cada dado pertence a uma corretora; configurações (IA, e-mail diário) passam a ser por corretora.
   Obrigatório antes de vender para uma segunda corretora.

## Produto

1. **Idiomas (pt-BR e en, depois outros):** tirar os textos fixos das telas e dos e-mails (i18n do Angular).
2. **App mobile:** trazer as telas novas (comissão, configurações, usuários).
3. Excluir os dados de demonstração do banco de desenvolvimento.
4. Atualizar o `scripts/smoke_test.py` (ainda espera "Nº da apólice" obrigatório).

## Infraestrutura

1. Quando a máquina ARM (A1) da Oracle sair: migrar (backup → deploy na A1 → restaurar → trocar o IP no DuckDNS).
2. Configurar no servidor: e-mail diário (SMTP) e cópia do backup para o Google Drive.
