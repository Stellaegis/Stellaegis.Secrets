# Third-party notices

Stellaegis.Secrets is licensed under the MIT License (see [LICENSE](LICENSE)).

Its packages contain no third-party code. They reference the packages below, which NuGet installs
alongside them, each under its own license.

| Component | Used by | License (SPDX) | Project |
| --- | --- | --- | --- |
| Stellaegis.Primitives | `Stellaegis.Secrets` | MIT | <https://github.com/Stellaegis/Stellaegis.Primitives> |
| Dapper | `Stellaegis.Secrets.PostgreSQL` | Apache-2.0 | <https://github.com/DapperLib/Dapper> |
| Npgsql | `Stellaegis.Secrets.PostgreSQL` | PostgreSQL | <https://github.com/npgsql/npgsql> |
| Microsoft.Extensions.\*, ASP.NET Core | all packages | MIT | <https://github.com/dotnet> |

Test-only dependencies (xUnit, ASP.NET Core TestHost) are not part of any
package and are not listed here.
