@functional
Feature: Layout stability

  The form should not shuffle itself about — neither when it reports a problem nor
  between the controls of a row. These are measured in the browser, since they are
  claims about where things sit rather than about what they say.

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
