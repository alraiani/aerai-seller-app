---
name: add-vertical-feature
description: Scaffold a new feature end-to-end across all layers of the AERai Seller App (Domain, Application, Infrastructure, Presentation, Wpf) in the correct order and dependency direction. Use whenever adding a new business capability (a new screen, a new data type, a new sync target) to the app, not for one-off bug fixes.
---

# Add a vertical feature

Scaffolds a new feature through every layer of the app in the order the architecture requires. Read `CLAUDE.md` at the repo root first — this skill enforces the rules documented there.

## Order of operations

Work strictly in this order; do not write UI or Infrastructure code before the layers underneath exist.

1. **Domain** (`AERai.Seller.Domain`): add/extend the entity, enum, or value object this feature needs. No project references allowed from here — plain C# only.
2. **Application** (`AERai.Seller.Application`): define the interface(s) the feature needs (e.g. `IReplenishmentPlanner`, a repository interface) and the service/use-case implementing the business logic against those interfaces. Application may reference `Domain` and `SpApiClient`'s public contracts, nothing UI- or EF Core-specific.
3. **Infrastructure** (`AERai.Seller.Infrastructure`): implement the Application-layer interfaces — EF Core repository implementations (add a migration if the schema changed), or adapters that call `AERai.Seller.SpApiClient` typed clients and map responses into Domain shapes. If this feature needs a new SP-API call, use the `add-sp-api-endpoint` skill first.
4. **Presentation** (`AERai.Seller.Presentation`): add the ViewModel using `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`). It depends only on `Application` interfaces (injected via constructor), never on `Infrastructure` concretes or any WPF/WPF-UI type.
5. **Wpf** (`AERai.Seller.Wpf`): add the View (XAML `Page`/`UserControl`) bound to the ViewModel via DI-resolved `DataContext`. Code-behind is view-wiring only. If this is a new top-level section, add it to the `NavigationView` sidebar; if it's a Dashboard widget, add it as a new tile without restructuring the existing page.
6. **DI registration**: register the new interface → implementation mapping and the new ViewModel/Page in the composition root (`App.xaml.cs`).
7. **Tests**: add unit tests for the new Application-layer logic in `AERai.Seller.Application.Tests` (pure logic, no live API/DB calls).

## Guardrails

- Never let `Domain` reference anything else in the solution.
- Never let `Presentation` or `Wpf` contain business logic — if you're writing an `if` that decides business outcomes (reorder thresholds, fee categorization, forecast math), it belongs in `Application`.
- `Wpf` has a project reference to `Infrastructure`/`SpApiClient` for DI registration only (composition-root exception, see `CLAUDE.md`). Never use an `Infrastructure` or `SpApiClient` type from a View, code-behind, or anywhere in `Wpf` outside `App.xaml.cs`'s service registration.
- If the feature touches SP-API, all calls go through `SpApiRequestPipeline` (see `add-sp-api-endpoint`) — never a new standalone `HttpClient`.
- When done, consider running the `architecture-reviewer` agent to check the change against these rules before calling the feature complete.
