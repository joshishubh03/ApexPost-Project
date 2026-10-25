# Application data

The application creates branches.json, staff.json, products.json, shipping-zones.json, and shipping-rates.json here on first run in Development. The staff data contains password hashes only. Product data includes 30 demo catalogue records; shipping data includes 10 zones with Standard and Express rates. `inventory.json` is created when the first stock adjustment is saved, `counter-transactions.json` when the first counter sale is recorded, `money-orders.json` when a money order is issued, `identity-verifications.json` when a threshold-triggered check is attempted, and `audit-records.json` when an audited operation is completed. Identity verification records contain a masked identifier only.

This is local training data only. Do not store production records, secrets, or real identity document numbers here.
