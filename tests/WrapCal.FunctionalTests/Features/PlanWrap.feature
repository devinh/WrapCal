@functional
Feature: Wrap planning

  The calculator: enter a roll and boxes, get a cut plan and a paper layout. Every
  scenario states the roll and boxes it uses, so the expected results can be checked
  against them without knowing what the page defaults to.

  Scenario: Plan a single box
    Given a roll 30 in wide with an overlap of 0.5
    And a box "Book" measuring 8 by 6 by 2
    When I plan the wrap
    Then I should see a cut plan
    And I should see the paper layout

  Scenario: Plan three boxes
    Given a roll 30 in wide with an overlap of 0.5
    And a box "Book" measuring 8 by 6 by 2
    And a box "Mug" measuring 5 by 5 by 5
    And a box "Shirt" measuring 12 by 9 by 2
    When I plan the wrap
    Then I should see a cut plan with 3 sections

  Scenario: A box too big for the roll is rejected
    Given a roll 30 in wide with an overlap of 0.5
    And a box "Huge" measuring 500 by 500 by 500
    When I plan the wrap
    Then I should see an error instead of a plan
    And I should not see the paper layout

  Scenario: A box with an impossible dimension is stopped before planning
    Given a roll 30 in wide with an overlap of 0.5
    And a box "Flat" measuring 0 by 6 by 1.5
    When I try to plan the wrap
    Then I should see a validation message about the length
    And I should not see the paper layout

  Scenario: Results follow the selected units
    Given a roll 50 cm wide with an overlap of 0.5
    And a box "Book" measuring 20 by 15 by 4
    When I plan the wrap
    Then the results should be reported in "cm"

  Scenario: Removing a box removes its cut
    Given a roll 30 in wide with an overlap of 0.5
    And a box "Book" measuring 8 by 6 by 2
    And a box "Mug" measuring 5 by 5 by 5
    When I remove the last box
    And I plan the wrap
    Then I should see a cut plan with 1 section

  Scenario Outline: The roll orientation can point each way
    Given a roll 30 in wide with an overlap of 0.5
    And a box "Book" measuring 8 by 6 by 2
    When I plan the wrap
    And I set the roll orientation to <direction>
    Then the paper layout should say the roll continues past the <edge> edge

    Examples:
      | direction  | edge   |
      | Top        | top    |
      | Bottom     | bottom |
      | Right      | right  |
      | Left       | left   |
