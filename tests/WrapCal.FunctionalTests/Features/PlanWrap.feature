@functional
Feature: Wrap planning

  The calculator: enter a roll and boxes, get a cut plan and a paper layout.

  Scenario: Plan a single box
    When I plan the default box
    Then I should see a cut plan
    And I should see the paper layout

  Scenario: Plan several boxes
    When I plan 3 boxes
    Then I should see a cut plan with 3 sections

  Scenario: A box too big for the roll is rejected
    When I plan a box far larger than the roll
    Then I should see an error instead of a plan
    And I should not see the paper layout

  Scenario: A box with an impossible dimension is stopped before planning
    When I plan a box with zero length
    Then I should see a validation message about the length
    And I should not see the paper layout

  Scenario: Showing a validation message does not move the page around
    When I break a box dimension
    Then the Plan button should not have moved

  Scenario: Results follow the selected units
    When I plan using centimetres
    Then the results should be reported in "cm"

  Scenario: Removing a box removes its cut
    When I add a second box, remove it, then plan
    Then I should see a cut plan with 1 section

  Scenario Outline: The roll direction can point each way
    When I plan and set the roll to continue <direction>
    Then the paper layout should say the roll continues past the <edge> edge

    Examples:
      | direction  | edge   |
      | Upwards    | top    |
      | Downwards  | bottom |
      | Rightwards | right  |
      | Leftwards  | left   |
