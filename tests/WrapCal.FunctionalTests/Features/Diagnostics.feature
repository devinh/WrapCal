@functional
Feature: Diagnostics

  A JS interop call that fails is deliberately swallowed: the theme falls back to its defaults and
  the drawing simply cannot be zoomed. Swallowing it silently is what would make a failure
  untraceable, so each one is written to the browser console. That the line arrives there at all is
  something only a real browser can settle — where `Console.Out` and `Console.Error` land is the
  runtime's decision, not the app's, and no component test has a console to land in.

  Scenario: A broken diagram script is reported in the console
    Given the diagram script is broken before the page loads
    And a roll 30 in wide with an overlap of 0.5
    And a box "Book" measuring 8 by 6 by 2
    When I plan the wrap
    Then I should see the paper layout
    And the console should report the failed diagram call
