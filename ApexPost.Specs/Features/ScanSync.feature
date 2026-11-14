@device
Feature: Offline scan synchronisation
  As a courier who often has no network
  I want every scan saved on my phone first and uploaded when the connection returns
  So that no pickup or delivery is lost, and my phone ends up agreeing with the server

  Runs at service level: the real device outbox and an embedded Couchbase Lite store on one side, the real
  shipment service rules on the other, connected in-process. The rules are about device state and server
  de-duplication, not HTTP, so no network is involved.

  Background:
    Given shipment "APX-60000001" has been registered
    And courier "courier-007" is working without a network connection

  Scenario: Scans taken offline are uploaded when the courier is back online
    When the courier records a "Pickup" scan for "APX-60000001" at "Maplewood Depot"
    And the courier records an "InTransit" scan for "APX-60000001" at "North Hub"
    Then 2 scans are waiting to be synced on the phone
    When the connection comes back and the phone syncs
    Then no scans are waiting to be synced on the phone
    And the server holds 2 scans for "APX-60000001"
    And shipment "APX-60000001" is now "InTransit"

  Scenario: A scan the server already has is skipped, not stored twice
    Given the courier's "Pickup" scan for "APX-60000001" reached the server but the phone never got the answer
    When the connection comes back and the phone syncs
    Then the server reports 1 duplicate scan skipped
    And the server holds 1 scan for "APX-60000001"
    And no scans are waiting to be synced on the phone

  Scenario: While the server is unreachable nothing is lost
    When the courier records a "Pickup" scan for "APX-60000001" at "Maplewood Depot"
    And the phone tries to sync while still offline
    Then 1 scan is waiting to be synced on the phone
    And the server holds 0 scans for "APX-60000001"

  Scenario Outline: A scan that breaks the delivery order is rejected and kept on the phone for review
    When the courier records a "<scan type>" scan for "APX-60000001" at "Front door"
    And the connection comes back and the phone syncs
    Then the phone marks the scan as failed because "Invalid status transition"
    And shipment "APX-60000001" is still "Created"

    Examples:
      | scan type | why it is wrong                         |
      | InTransit | the parcel was never picked up          |
      | Delivered | cannot deliver a parcel never picked up |
