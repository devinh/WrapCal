@functional
Feature: Wrap planning

  A user enters a roll and a box, and gets back a cut plan.

  Scenario: Plan a single box
    When I plan a single box
    Then I should see a cut plan
    And I should see the paper layout
