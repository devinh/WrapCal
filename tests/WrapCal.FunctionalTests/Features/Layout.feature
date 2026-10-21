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

  Scenario: The box name lines up with the other box inputs
    Given the plan page
    Then the box name input should line up with the other inputs

  Scenario: The roll direction menu fits its longest option
    Given a roll 30 cm wide with an overlap of 0.5
    And a box "Book" measuring 9 by 6 by 1.5
    When I plan the wrap
    Then every roll direction option should fit in its menu

  Scenario: Ctrl and the mouse wheel enlarge the drawing
    Given a roll 30 cm wide with an overlap of 0.5
    And 2 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I note the drawing's scale
    And a Ctrl wheel of one notch arrives over the diagram
    Then the zoom should read 150%
    And the drawing's scale should match the zoom

  Scenario: A trackpad pinch zooms as much in small turns as in one
    # A trackpad sends a stream of small deltas where a mouse sends one notch. Scaling by a ratio
    # means four quarter-turns compose to exactly one notch, which is what makes it smooth.
    Given a roll 30 cm wide with an overlap of 0.5
    And 2 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I note the drawing's scale
    And a Ctrl wheel of four small turns arrives over the diagram
    Then the zoom should read 150%
    And the drawing's scale should match the zoom

  Scenario: A Safari gesture pinch enlarges the drawing
    # Safari reports a trackpad pinch as gesture events carrying a scale, not as ctrl+wheel, and
    # the spread is damped so it answers no more eagerly than a wheel does elsewhere.
    Given a roll 30 cm wide with an overlap of 0.5
    And 2 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I note the drawing's scale
    And a trackpad pinch of 225 percent arrives
    Then the zoom should read 150%
    And the drawing's scale should match the zoom

  Scenario Outline: Zooming keeps the drawing under the pointer
    # Up and Left lay the scroller out from the far end, so a scroll correction runs the other way
    # there — and Up is the default, which makes the reversed case the common one.
    #
    # Twelve boxes rather than three: the correction is a scroll correction, and a short sideways
    # plan is smaller than its frame, so there would be nothing to scroll and nothing to test.
    Given a roll 30 cm wide with an overlap of 0.5
    And 12 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll orientation to <direction>
    And I note the cut under the pointer
    And a Ctrl wheel of one notch arrives over the diagram
    Then the zoom should read 150%
    And the cut under the pointer should not have moved

    Examples:
      | direction |
      | Top       |
      | Bottom    |
      | Left      |
      | Right     |

  Scenario Outline: A zoomed sideways plan scrolls end to end
    Given a roll 30 cm wide with an overlap of 0.5
    And 12 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll orientation to <direction>
    And a Ctrl wheel of one notch arrives over the diagram
    Then the drawing should be scrollable across its whole width

    Examples:
      | direction |
      | Left      |
      | Right     |

  Scenario Outline: Switching orientation returns to the origin
    Given a roll 30 cm wide with an overlap of 0.5
    And 12 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll orientation to <from>
    And I scroll the drawing to the middle
    And I set the roll orientation to <to>
    Then the origin should be in view

    Examples:
      | from | to    |
      | Top  | Left  |
      | Left | Top   |
      | Top  | Right |
      | Left | Bottom |

  Scenario Outline: Switching orientation resets the zoom too
    Given a roll 30 cm wide with an overlap of 0.5
    And 12 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll orientation to <from>
    And a Ctrl wheel of one notch arrives over the diagram
    And I set the roll orientation to <to>
    Then the zoom should read 100%

    Examples:
      | from | to    |
      | Top  | Left  |
      | Left | Top   |
      | Top  | Right |

  Scenario Outline: The drawing hugs the left of its frame
    Given a roll 30 cm wide with an overlap of 0.5
    And 2 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll orientation to <direction>
    Then the drawing should sit at the left of its frame

    Examples:
      | direction |
      | Left      |
      | Right     |

  Scenario: The sign-off sits in the middle of the page
    Given the plan page
    Then the sign-off should be centered

  Scenario: The option descriptions line up across the row
    Given the plan page
    Then the option descriptions should all line up

  @phone
  Scenario: A phone keeps the box row and the cut plan inside their cards
    Given a phone's width
    And a roll 80 cm wide with an overlap of 0.5
    And 3 identical boxes measuring 30 by 20 by 15
    Then the Remove button should share a line with the box's dimensions
    When I plan the wrap
    Then the cut plan table should fit inside its card
    And the page should not scroll sideways

  Scenario Outline: Adding boxes does not shrink the drawing
    # The guard that does not depend on the plan being long. The scale comes from the roll
    # width, so the paper's extent across the roll is fixed — and a drawing that were scaled to
    # fit its frame would shrink as boxes arrived. The scroll scenario below depends on the plan
    # outgrowing the frame, which better packing can take away; this one cannot be taken away.
    Given a roll 30 cm wide with an overlap of 0.5
    And 2 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll orientation to <Direction>
    And I note the drawing's scale
    And I add 10 boxes measuring 8 by 6 by 4
    And I plan the wrap
    Then the drawing's scale should be unchanged

    Examples:
      | Direction |
      | Top       |
      | Right     |

  Scenario Outline: A long plan scrolls along the roll instead of shrinking to fit
    # 24 boxes, not 12: the packer learned to lay identical boxes two to a shelf, which halved
    # this plan and left it short enough to fit — a plan that fits proves nothing about whether
    # a long one is shrunk or scrolled.
    Given a roll 30 cm wide with an overlap of 0.5
    And 24 identical boxes measuring 8 by 6 by 4
    When I plan the wrap
    And I set the roll orientation to <Direction>
    Then the layout should be cut off by its box rather than shrunk to fit it
    And the origin should be in view

    Examples:
      | Direction  |
      | Top        |
      | Bottom     |
      | Left       |
      | Right      |
