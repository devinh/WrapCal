@functional
Feature: Wrap planning

  The calculator: enter a roll and boxes, get a cut plan and a paper layout. Every
  scenario sets the form up first, then does one thing to it, then checks the result.

  Scenario: Plan a single box
    Given the plan page
    When I plan the wrap
    Then I should see a cut plan
    And I should see the paper layout

  Scenario: Plan three boxes
    Given the plan page
    And a box "Book" measuring 8 by 6 by 2
    And a box "Mug" measuring 5 by 5 by 5
    And a box "Shirt" measuring 12 by 9 by 2
    When I plan the wrap
    Then I should see a cut plan with 3 sections

  Scenario: A box too big for the roll is rejected
    Given the plan page
    And a box "Huge" measuring 500 by 500 by 500
    When I plan the wrap
    Then I should see an error instead of a plan
    And I should not see the paper layout

  Scenario: A box with an impossible dimension is stopped before planning
    Given the plan page
    And a box "Flat" measuring 0 by 6 by 1.5
    When I try to plan the wrap
    Then I should see a validation message about the length
    And I should not see the paper layout

  Scenario: Results follow the selected units
    Given the plan page
    And the units are set to centimetres
    When I plan the wrap
    Then the results should be reported in "cm"

  Scenario: Removing a box removes its cut
    Given the plan page
    And a box "Book" measuring 8 by 6 by 2
    And a box "Mug" measuring 5 by 5 by 5
    When I remove the last box
    And I plan the wrap
    Then I should see a cut plan with 1 section

  Scenario Outline: The roll direction can point each way
    Given the plan page
    When I plan the wrap
    And I set the roll to continue <direction>
    Then the paper layout should say the roll continues past the <edge> edge

    Examples:
      | direction  | edge   |
      | Upwards    | top    |
      | Downwards  | bottom |
      | Rightwards | right  |
      | Leftwards  | left   |
