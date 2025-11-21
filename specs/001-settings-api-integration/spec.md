# Feature Specification: Settings API Integration for Dashboard

**Feature Branch**: `001-settings-api-integration`
**Created**: 2025-11-21
**Status**: Draft
**Input**: Settings API Integration - Load van diagram selection from Settings API on Dashboard

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Van Diagram Based on Saved Settings (Priority: P1)

As a van owner, when I open the Dashboard, I want to see the van diagram that matches my van model (which I previously configured in Settings) so that the visual representation accurately reflects my actual van layout.

**Why this priority**: This is the core value proposition - showing the correct van diagram is essential for the Dashboard to be useful. Without this, users see a generic diagram that doesn't match their van, reducing trust and usability of the entire system.

**Independent Test**: Can be fully tested by (1) configuring a van model in Settings, (2) navigating to Dashboard, (3) verifying the correct diagram image loads. Delivers immediate value by showing personalized van layout.

**Acceptance Scenarios**:

1. **Given** user has selected "Mercedes Sprinter LWB" in Settings, **When** user navigates to Dashboard, **Then** Mercedes Sprinter LWB diagram is displayed
2. **Given** user has selected "Ford Transit" in Settings, **When** user navigates to Dashboard, **Then** Ford Transit diagram is displayed
3. **Given** user changes van model from "Mercedes Sprinter" to "VW Crafter" in Settings, **When** user returns to Dashboard, **Then** VW Crafter diagram is displayed without refresh
4. **Given** Settings API returns a van model, **When** Dashboard loads, **Then** van diagram loads within 500ms (per Constitution Principle II)

---

### User Story 2 - Fallback to Default Diagram on Error (Priority: P2)

As a van owner, if the Settings API is unavailable or returns an error, I want the Dashboard to display a default van diagram so that I can still monitor my van systems rather than seeing a broken page.

**Why this priority**: Error handling is critical for offline-first operation (Constitution Principle III). The system must remain functional even when configuration cannot be loaded.

**Independent Test**: Can be tested by simulating Settings API failure and verifying Dashboard still loads with default diagram. Demonstrates system resilience.

**Acceptance Scenarios**:

1. **Given** Settings API is unavailable (503 error), **When** Dashboard loads, **Then** default Mercedes Sprinter LWB diagram is displayed
2. **Given** Settings API returns invalid data, **When** Dashboard loads, **Then** default diagram is displayed and error is logged
3. **Given** Settings API request times out, **When** Dashboard loads within 2 seconds, **Then** default diagram is displayed
4. **Given** Settings API error occurs, **When** user later navigates to Settings page, **Then** they can see and correct their van model selection

---

### User Story 3 - Real-Time Diagram Updates (Priority: P3)

As a van owner, when I change my van model in Settings while Dashboard is open in another browser tab, I want the Dashboard to update the diagram automatically so that I don't have to manually refresh.

**Why this priority**: This enhances user experience by leveraging the existing SignalR real-time infrastructure, but is not essential for basic functionality. Users can manually refresh if needed.

**Independent Test**: Can be tested by opening Dashboard and Settings in separate tabs, changing van model in Settings, and observing Dashboard update automatically.

**Acceptance Scenarios**:

1. **Given** Dashboard is open and Settings page is open in another tab, **When** user changes van model in Settings and saves, **Then** Dashboard diagram updates within 1 second
2. **Given** multiple clients are viewing Dashboard, **When** settings are changed on one client, **Then** all clients update their diagrams
3. **Given** SignalR connection is temporarily down, **When** settings change occurs, **Then** Dashboard displays stale diagram until reconnection (graceful degradation)

---

### Edge Cases

- What happens when Settings API returns a van model name that doesn't have a corresponding diagram image file?
  - **Fallback**: Display default diagram and log warning. Settings page should validate van model selection against available diagrams.

- How does system handle race conditions where Settings API call completes after initial Dashboard load?
  - **Resolution**: Dashboard should accept late-arriving settings and update diagram if still showing default.

- What happens if user manually edits Settings JSON file to include invalid van diagram path?
  - **Validation**: Settings API should validate VanDiagram field against available files before persisting. Return validation error if invalid.

- How does Dashboard behave during slow network conditions (>2 second API response)?
  - **Progressive Loading**: Show loading skeleton for diagram area, timeout after 2 seconds and show default diagram, continue to listen for late response.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-1**: Dashboard page MUST call the Settings API (GET /api/settings) during initialization to retrieve current system settings including van diagram selection

- **FR-2**: Dashboard MUST display the van diagram image corresponding to the VanDiagram field returned from Settings API

- **FR-3**: Dashboard MUST display default Mercedes Sprinter LWB diagram if Settings API call fails or returns no van diagram selection

- **FR-4**: Dashboard MUST log errors when Settings API call fails, including error type and timestamp to browser console

- **FR-5**: The TODO comment at line 256 of src/Frontend/VanDaemon.Web/Pages/Index.razor MUST be removed and replaced with actual Settings API integration code

### Non-Functional Requirements

- **NFR-1**: Settings API call and diagram loading MUST complete within 500ms under normal network conditions (per Constitution Principle II)

- **NFR-2**: Dashboard MUST function when Settings API is unavailable (per Constitution Principle III - Offline-First)

- **NFR-3**: Implementation MUST comply with VanDaemon Constitution v1.0.0 Principle V (Clean Architecture - no business logic in Razor components)

### Key Entities

- **SystemSettings**: Returned from GET /api/settings endpoint. Contains VanModel (string) and VanDiagram (string - relative path to diagram image file)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-1**: 100% of users with configured van model see their selected diagram on Dashboard load

- **SC-2**: 95% of diagram loads complete within 500ms on local network

- **SC-3**: 0 instances of Dashboard failing to load due to Settings API errors

- **SC-4**: TODO comment removed and feature passes all automated tests

- **SC-5**: Feature verified against Constitution Principles II, III, and V with no violations

## Out of Scope

The following are explicitly **not** included in this feature:

- Van diagram management (adding, removing, or modifying available van diagram images)
- Custom diagram upload by users
- Interactive diagram overlays (positioning tank/control indicators on diagram)
- Diagram editing capabilities
- Settings page changes or API modifications
- Multiple diagrams per van (top, side, interior views)
- Advanced diagram caching strategies

## Assumptions

1. Settings API (GET /api/settings) already exists, returns SystemSettings with VanDiagram field, and is tested
2. Van diagram image files referenced in VanDiagram field already exist in wwwroot/images/ directory
3. Users can already select and save van models via Settings page
4. Dashboard component already has access to configured HttpClient instance
5. Default diagram file (/images/Mercedes_Sprinter_LWB_Camper.png) exists in frontend project
6. Settings API requires no modifications for this feature
7. Van diagrams are PNG or SVG images suitable for web display
8. Van model selection changes infrequently (not performance-critical path)

## Dependencies

### Internal Dependencies
- Settings API: GET /api/settings endpoint must be operational
- Settings Service: Backend SettingsService must return valid SystemSettings with VanDiagram field
- Image Assets: Van diagram image files must exist in wwwroot/images/ directory

### External Dependencies
- HttpClient: Blazor WebAssembly HttpClient configured in Program.cs
- Browser Image Loading: Standard browser img tag rendering

## Technical Constraints

1. Blazor WebAssembly lifecycle: Settings API call must be made in appropriate lifecycle method (OnInitializedAsync)
2. Existing code structure: Must work within current Index.razor component structure without major refactoring
3. Error handling pattern: Must follow existing error handling patterns in codebase (try-catch with console logging)
4. HttpClient reuse: Must use existing injected HttpClient instance
5. No state management library: Direct component state update (no Fluxor or Redux per PROJECT_PLAN.md)

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Settings API slow on Raspberry Pi | Medium | Medium | Implement 2-second timeout, use default diagram on timeout |
| Invalid VanDiagram path in settings | Low | Low | Validate paths in Settings API before persisting, fallback to default |
| Race condition with SignalR | Low | Low | Accept late-arriving settings, update diagram if still showing default |
| Image file missing | Low | Medium | Validate image exists before allowing selection in Settings page |
| Broken image on mobile | Low | Medium | Test responsive image display during implementation |

## Open Questions

None. All requirements are clear based on existing codebase structure and constitution requirements.
