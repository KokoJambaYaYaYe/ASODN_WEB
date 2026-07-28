# Apply migration to database 
dotnet ef database update ` -c "AuthSystemDataBaseDBContext" ` -s "..\..\authSystem.BFF" -p "..\..\AuthCommon.Database.Infrastructure" ` --verbose