# Application data

The application creates branches.json, staff.json, products.json, shipping-zones.json, and shipping-rates.json here on first run in Development. The staff data contains password hashes only. Product data includes 30 demo catalogue records; shipping data includes 10 zones with Standard and Express rates. `inventory.json` is created when the first stock adjustment is saved, and `counter-transactions.json` when the first counter sale is recorded. Seed records are deterministic apart from salted password hashes.

This is local training data only. Do not store production records, secrets, or real identity document numbers here.
