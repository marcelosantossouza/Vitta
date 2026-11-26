# Backlog do Vitta

Melhorias combinadas e ainda não feitas. Ordem dentro de cada bloco = prioridade sugerida.

## Prospecção

Feito: importação como lote (histórico + excluir), repetidos com opção de completar, campos de perfil e renda
(endereço, cidade, UF, CEP, telefone 2, nascimento, profissão/órgão, salário, descontos, empréstimos), lista com
filtros e ações em lote, dashboard com os mesmos filtros.

1. **Cuidado LGPD:** salário, descontos e empréstimos são dados pessoais financeiros. Registrar a origem de cada lista
   (base legal) e limitar quem vê esses campos (ex.: perfil Assistente sem acesso a dados financeiros).
2. **Campos personalizados** também na prospecção (colunas sem campo próprio hoje vão para "Outras informações",
   visíveis mas não filtráveis).
3. Exportar a lista filtrada (planilha) e filtros salvos ("minhas listas").

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
