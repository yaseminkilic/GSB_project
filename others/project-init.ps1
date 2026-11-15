# project init
dotnet new mvc -n SportsEvents.Mvc -f net10.0
cd SportsEvents.Mvc

# EF Core paketleri ekle
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version "10.0.*"
dotnet add package Microsoft.EntityFrameworkCore.Design --version "10.0.*"
dotnet ef --version
# dotnet tool update --global dotnet-ef --version "10.0.*"

# EF Core ile veritabanı oluşturma - scaffold
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef dbcontext scaffold `
    "Name=ConnectionStrings:SportsEvents" `
    Microsoft.EntityFrameworkCore.SqlServer `
    --context SportsEventsDbContext `
    --context-dir Data `
    --output-dir Entities `
    --table dbo.Event `
    --table dbo.Participant `
    --table dbo.Registration `
    --no-onconfiguring
dotnet build
dotnet run