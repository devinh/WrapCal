@functional
Feature: Layout stability

  The form should not shuffle itself about — neither when it reports a problem nor
  between the controls of a row — and the paper layout should meet the edge of its
  box rather than shrink away from it. These are measured in the browser, since they
  are claims about where things sit and how big they are rather than about what they say.

  Scenario: Reporting a validation message does not move the page
    Given the plan page
    When a box dimension becomes invalid
    Then the Plan button should not have moved

  Scenario: The Remove button lines up with the box inputs
    Given the plan page
    Then the Remove button should line up with the row's inputs

  Scenario: The option descriptions line up across the row
    Given the plan page
    Then the option descriptions should all line up

  Scenario Outline: A long plan scrolls along the roll instead of shrinking to fit
    Given a roll 30 cm wide with an overlap of 0.5
    And 12 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll to continue <Direction>
    Then the layout should be cut off by its box rather than shrunk to fit it
    And the origin should be in view

    Examples:
      | Direction  |
      | Upwards    |
      | Downwards  |
      | Leftwards  |
      | Rightwards |
