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

  Scenario: The Remove button lines up with the box inputs
    When I open the plan page
    Then the Remove button should line up with the row's inputs

  Scenario: The option descriptions line up across the row
    When I open the plan page
    Then the option descriptions should all line up

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

  # Spec tables: the inputs a user would type and the plan they should get back.
  # Roll length is what the page shows, to one decimal.
  Scenario Outline: A single box produces a known plan
    When I plan a roll of <roll> <unit> wide with an overlap of <overlap>
    And I enter a box "<name>" measuring <l> by <w> by <h>
    And I calculate the plan
    Then the plan should need <rollLength> of roll
    And the plan should show <sections> cut section
    And the plan should list the box "<name>"

    Examples:
      | roll | unit | overlap | name   | l  | w  | h  | rollLength | sections |
      | 100  | cm   | 0       | Cube   | 20 | 20 | 20 | 40.0       | 1        |
      | 60   | cm   | 0.5     | Book   | 22 | 15 | 4  | 37.5       | 1        |
      | 50   | cm   | 1       | Crate  | 30 | 20 | 10 | 101.0      | 1        |
      | 30   | in   | 0.5     | Gadget | 10 | 5  | 2  | 14.5       | 1        |

  Scenario Outline: Several boxes produce a known plan
    When I plan a roll of <roll> <unit> wide with an overlap of <overlap>
    And I enter a box "<name1>" measuring <l1> by <w1> by <h1>
    And I enter a box "<name2>" measuring <l2> by <w2> by <h2>
    And I calculate the plan
    Then the plan should need <rollLength> of roll
    And the plan should show <sections> cut sections
    And the plan should list the box "<name1>"
    And the plan should list the box "<name2>"

    Examples:
      | roll | unit | overlap | name1 | l1 | w1 | h1 | name2 | l2 | w2 | h2 | rollLength | sections |
      | 60   | cm   | 0.5     | Book  | 22 | 15 | 4  | Mug   | 10 | 10 | 10 | 58.0       | 2        |
      | 80   | cm   | 1       | Crate | 30 | 20 | 10 | Cube  | 20 | 20 | 20 | 132.0      | 2        |
      | 100  | cm   | 0.5     | Shirt | 15 | 10 | 2  | Frame | 40 | 30 | 20 | 100.5      | 2        |
