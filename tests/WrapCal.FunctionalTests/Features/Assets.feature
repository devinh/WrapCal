@functional
Feature: Styling assets

  The app is styled by MudBlazor alone. Nothing here is visible to a user, which is exactly
  why it is worth asserting: a half-finished migration that leaves Bootstrap linked still
  looks plausible in a browser, and the two stylesheets quietly fight over every control.

  Scenario: Bootstrap is gone
    Given the plan page
    Then no Bootstrap stylesheet should be linked
    And the old Bootstrap file should no longer be served

  Scenario: MudBlazor is doing the styling
    Given the plan page
    Then the MudBlazor stylesheet should be linked
    And the MudBlazor theme should be applied
