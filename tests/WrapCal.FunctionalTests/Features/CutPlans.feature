@functional
Feature: Cut plans

  Concrete inputs and the plan they should produce, driven through the real page.
  Each sheet is asserted by name rather than by position, since the packer's order is
  its own business.

  Scenario: A single cube
    Given a roll 100 cm wide with an overlap of 0
    And a box "Cube" measuring 20 by 20 by 20
    When I plan the wrap
    Then the cuts should be:
      | name | width | length |
      | Cube | 80.0  | 40.0   |
    And the cut plan should use 40.0 of roll
    And the cut plan should report 1 cut

  Scenario: A single book
    Given a roll 60 cm wide with an overlap of 0.5
    And a box "Book" measuring 22 by 15 by 4
    When I plan the wrap
    Then the cuts should be:
      | name | width | length |
      | Book | 52.5  | 37.5   |
    And the cut plan should use 37.5 of roll

  Scenario: A crate that only fits one way
    Given a roll 50 cm wide with an overlap of 1
    And a box "Crate" measuring 30 by 20 by 10
    When I plan the wrap
    Then the cuts should be:
      | name  | width | length |
      | Crate | 41.0  | 101.0  |
    And the cut plan should use 101.0 of roll

  Scenario: A box measured in inches
    Given a roll 30 in wide with an overlap of 0.5
    And a box "Gadget" measuring 10 by 5 by 2
    When I plan the wrap
    Then the cuts should be:
      | name   | width | length |
      | Gadget | 15.5  | 14.5   |
    And the cut plan should use 14.5 of roll

  Scenario: Two boxes side by side
    Given a roll 60 cm wide with an overlap of 0.5
    And a box "Book" measuring 22 by 15 by 4
    And a box "Mug" measuring 10 by 10 by 10
    When I plan the wrap
    Then the cuts should be:
      | name | width | length |
      | Book | 52.5  | 37.5   |
      | Mug  | 40.5  | 20.5   |
    And the cut plan should use 58.0 of roll
    And the cut plan should report 2 cuts

  Scenario: A crate and a cube
    Given a roll 80 cm wide with an overlap of 1
    And a box "Crate" measuring 30 by 20 by 10
    And a box "Cube" measuring 20 by 20 by 20
    When I plan the wrap
    Then the cuts should be:
      | name  | width | length |
      | Crate | 61.0  | 51.0   |
      | Cube  | 41.0  | 81.0   |
    And the cut plan should use 132.0 of roll
    And the cut plan should report 2 cuts

  Scenario: A frame and a shirt
    Given a roll 100 cm wide with an overlap of 0.5
    And a box "Shirt" measuring 15 by 10 by 2
    And a box "Frame" measuring 40 by 30 by 20
    When I plan the wrap
    Then the cuts should be:
      | name  | width | length |
      | Shirt | 17.5  | 50.5   |
      | Frame | 70.5  | 100.5  |
    And the cut plan should use 100.5 of roll
    And the cut plan should report 2 cuts
