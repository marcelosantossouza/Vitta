// Gera a mesma migration para todos os bancos suportados.
// Uso: npm run migrations:add -- NomeDaMigration
import { execSync } from 'node:child_process';

const name = process.argv[2];
if (!name) {
  console.error('Uso: npm run migrations:add -- <NomeDaMigration>');
  process.exit(1);
}

const providers = [
  { context: 'SqliteVittaDbContext', folder: 'Migrations/Sqlite' },
  { context: 'PostgresVittaDbContext', folder: 'Migrations/Postgres' },
];

for (const { context, folder } of providers) {
  console.log(`\n> ${context}`);
  execSync(
    `dotnet ef migrations add ${name} -p src/Vitta.Persistence -s src/Vitta.API --context ${context} -o ${folder}`,
    { stdio: 'inherit' },
  );
}
