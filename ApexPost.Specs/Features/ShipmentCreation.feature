@api
Feature: Shipment creation
  As the ApexPost operations team
  I want every parcel registered once, with a valid tracking number, weight and recipient
  So that couriers, customers and partners all track the same parcel

  Runs at API level: the business promises here ("created and trackable", "refused as a duplicate",
  "refused with a reason") are what a client of the shipment API actually sees.

  Background:
    Given I am signed in to the shipment API as an administrator

  Scenario: A valid shipment is created and can be tracked straight away
    When I create a shipment with tracking number "APX-50000001" weighing 2.5 kg for "Jordan Example" in "Maplewood"
    Then the shipment is created
    And the response tells me where to find shipment "APX-50000001"
    And tracking "APX-50000001" shows the status "Created"

  Scenario: The same tracking number cannot be registered twice
    Given a shipment with tracking number "APX-50000002" for "Jordan Example" already exists
    When I create a shipment with tracking number "APX-50000002" weighing 1.0 kg for "Sam Example" in "Riverton"
    Then the shipment is refused as a duplicate
    And shipment "APX-50000002" still belongs to "Jordan Example"

  Scenario Outline: Parcels outside the weight limits are refused with a reason
    When I create a shipment with tracking number "<tracking number>" weighing <weight> kg for "Jordan Example" in "Maplewood"
    Then the shipment is refused because "Weight must be between 0.1 and 70 kg."
    And no shipment "<tracking number>" exists

    Examples:
      | tracking number | weight | case                    |
      | APX-50000010    | 0.05   | lighter than a letter   |
      | APX-50000011    | 0      | no weight entered       |
      | APX-50000012    | 70.01  | just over the 70 kg cap |
      | APX-50000013    | 120    | pallet, not a parcel    |

  Scenario Outline: Parcels exactly at the weight limits are accepted
    When I create a shipment with tracking number "<tracking number>" weighing <weight> kg for "Jordan Example" in "Maplewood"
    Then the shipment is created

    Examples:
      | tracking number | weight |
      | APX-50000020    | 0.1    |
      | APX-50000021    | 70     |
