# Third-party notices

Stellaegis.Secrets is licensed under the MIT License (see [LICENSE](LICENSE)).

Its packages contain no third-party code. They reference the packages below, which NuGet installs
alongside them, each under its own license.

| Component | Used by | License (SPDX) | Project |
| --- | --- | --- | --- |
| Dapper | `Stellaegis.Secrets.PostgreSQL` | Apache-2.0 | <https://github.com/DapperLib/Dapper> |
| Npgsql | `Stellaegis.Secrets.PostgreSQL` | PostgreSQL | <https://github.com/npgsql/npgsql> |
| Microsoft.Extensions.\*, ASP.NET Core | all packages | MIT | <https://github.com/dotnet> |

Test-only dependencies (xUnit, Microsoft.NET.Test.Sdk, ASP.NET Core TestHost) are not part of any
package and are not listed here.
