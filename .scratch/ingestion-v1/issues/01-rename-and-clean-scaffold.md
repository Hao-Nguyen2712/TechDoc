# 01: Rename projects to TechDocAI.* and clean the scaffold

**What to build:** Prefactor so the rest of the pipeline lands in a correctly named, cruft-free solution: the projects become `TechDocAI.Api`, `TechDocAI.Core`, `TechDocAI.Infrastructure` (the naming the team's internal docs already use), template leftovers are removed, and the README matches. The app still builds and boots.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Solution builds with projects named `TechDocAI.Api`, `TechDocAI.Core`, `TechDocAI.Infrastructure`
- [x] Template leftovers (WeatherForecast controller, placeholder classes) removed; the API starts and serves its OpenAPI document
- [x] README and any docs references updated to the new project names
