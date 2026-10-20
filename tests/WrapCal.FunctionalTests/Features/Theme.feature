@functional
Feature: Theme

  The theme is a display preference — System by default, or pinned to light or dark — kept in a
  cookie, because an interactive component cannot write a response cookie. System defers to the
  operating system, which only the browser can report: that part needs a real media query, which
  no component test has.

  Scenario: The page follows a dark system
    Given the browser prefers a dark color scheme
    And the plan page
    Then the page should be in the dark theme

  Scenario: The page follows a light system
    Given the browser prefers a light color scheme
    And the plan page
    Then the page should be in the light theme

  Scenario: A page left open follows the system as it changes
    Given the browser prefers a light color scheme
    And the plan page
    Then the page should be in the light theme
    When the browser switches to a dark color scheme
    Then the page should be in the dark theme

  Scenario: An explicit choice wins over the system
    Given the browser prefers a dark color scheme
    And the plan page
    When I choose the light theme
    Then the page should be in the light theme

  Scenario: The cut layout is drawn in the page's theme
    Given the visitor last chose the dark theme
    And the plan page
    When I plan the wrap
    Then the cut layout should be drawn for a dark theme

  Scenario: The page is already in the right theme while the app is loading
    Given the visitor last chose the dark theme
    When the plan page is opened without letting the app load
    Then the loading page should be in the dark theme

  Scenario: The theme the cookie remembers is drawn from the first frame
    Given the visitor last chose the dark theme
    And the plan page
    Then the page should be in the dark theme

  Scenario: Choosing a theme is remembered for next time
    Given the plan page
    When I choose the dark theme
    Then the page should be in the dark theme
    When I reload the page
    Then the page should be in the dark theme
