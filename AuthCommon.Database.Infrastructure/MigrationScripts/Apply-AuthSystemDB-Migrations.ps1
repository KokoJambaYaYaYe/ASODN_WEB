dotnet ef database update `
  -c "AuthSystemDataBaseDBContext" `
  -s "..\..\authSystem.BFF" `
  -p "..\..\AuthCommon.Database.Infrastructure" `
  --connection "Host=192.168.95.133;Port=5432;Database=AuthSystemDB;Username=postgres;Password=1234567890" `
  --verbose