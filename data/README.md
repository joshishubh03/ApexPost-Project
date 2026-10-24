# Application data

The application creates branches.json, staff.json, and products.json here on first run in Development. The staff data contains password hashes only. Product data includes 30 demo catalogue records. `inventory.json` is created when the first stock adjustment is saved. Seed records are deterministic apart from salted password hashes.

This is local training data only. Do not store production records, secrets, or real identity document numbers here.
