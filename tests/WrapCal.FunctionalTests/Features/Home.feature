@functional
Feature: Home page

  The smoke test for the harness: it proves the app actually starts, Kestrel serves it,
  and a real browser can load it. Replace it with scenarios that exercise real behaviour
  once there is real UI.

  Scenario: Home page shows the app heading
    When I open the home page
    Then the page heading should read "WrapCal"
