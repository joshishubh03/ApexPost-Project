using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;

namespace ApexPost.Infrastructure.Seed;

public sealed class DemoDataSeeder(
    IBranchRepository branchRepository,
    IStaffRepository staffRepository,
    IProductRepository productRepository,
    IShippingZoneRepository shippingZoneRepository,
    IShippingRateRepository shippingRateRepository,
    IStaffPasswordService passwordService,
    string demoPassword)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var branches = await branchRepository.GetAllAsync(cancellationToken);
        if (branches.Count == 0)
        {
            branches = Enumerable.Range(1, 5)
                .Select(index => new Branch(
                    Guid.Parse($"10000000-0000-0000-0000-{index:000000000000}"),
                    $"AP-{index:000}",
                    $"ApexPost Branch {index}",
                    $"Region {index}",
                    isOpen: true))
                .ToArray();
            await branchRepository.SaveAllAsync(branches, cancellationToken);
        }

        var products = await productRepository.GetAllAsync(cancellationToken);
        if (products.Count == 0)
        {
            var productSeeds = new (string Sku, string Name, string Description, string Category, decimal Price)[]
            {
                ("POST-001", "Domestic Letter", "Standard domestic letter postage", "Postage", 25.00m),
                ("POST-002", "Large Letter", "Large format domestic letter postage", "Postage", 45.00m),
                ("POST-003", "Registered Letter", "Tracked registered letter service", "Postage", 85.00m),
                ("POST-004", "Express Letter", "Priority domestic letter service", "Postage", 120.00m),
                ("POST-005", "Postcard", "Domestic postcard postage", "Postage", 15.00m),
                ("POST-006", "International Letter", "Standard international letter postage", "Postage", 150.00m),
                ("POST-007", "International Express", "Priority international letter service", "Postage", 450.00m),
                ("POST-008", "Printed Matter", "Printed material postal service", "Postage", 35.00m),
                ("PACK-001", "Small Mailing Box", "Small corrugated mailing box", "Packaging", 40.00m),
                ("PACK-002", "Medium Mailing Box", "Medium corrugated mailing box", "Packaging", 65.00m),
                ("PACK-003", "Large Mailing Box", "Large corrugated mailing box", "Packaging", 95.00m),
                ("PACK-004", "Document Envelope", "Rigid document mailing envelope", "Packaging", 20.00m),
                ("PACK-005", "Padded Mailer", "Protective padded mailer", "Packaging", 30.00m),
                ("PACK-006", "Bubble Wrap Roll", "Protective bubble wrap roll", "Packaging", 110.00m),
                ("PACK-007", "Packing Tape", "Standard clear packing tape", "Packaging", 35.00m),
                ("PACK-008", "Shipping Labels", "Pack of adhesive shipping labels", "Packaging", 55.00m),
                ("RETL-001", "ApexPost Pen", "Branded blue ballpoint pen", "Retail", 20.00m),
                ("RETL-002", "ApexPost Notebook", "Branded pocket notebook", "Retail", 60.00m),
                ("RETL-003", "Address Book", "Hardcover address book", "Retail", 125.00m),
                ("RETL-004", "Greeting Card", "Blank all-occasion greeting card", "Retail", 40.00m),
                ("RETL-005", "Gift Wrap Sheet", "Decorative gift wrapping sheet", "Retail", 25.00m),
                ("RETL-006", "Postage Stamp Sheet", "Sheet of domestic postage stamps", "Retail", 100.00m),
                ("RETL-007", "Desk Calendar", "ApexPost annual desk calendar", "Retail", 85.00m),
                ("RETL-008", "Document Folder", "Reusable document folder", "Retail", 45.00m),
                ("SHIP-001", "Small Parcel Service", "Domestic small parcel handling", "Shipping", 90.00m),
                ("SHIP-002", "Medium Parcel Service", "Domestic medium parcel handling", "Shipping", 160.00m),
                ("SHIP-003", "Large Parcel Service", "Domestic large parcel handling", "Shipping", 280.00m),
                ("SHIP-004", "Fragile Handling", "Additional fragile parcel handling", "Shipping", 75.00m),
                ("MORD-001", "Money Order Fee - Basic", "Basic money order processing fee", "Money Order", 30.00m),
                ("MORD-002", "Money Order Fee - Express", "Express money order processing fee", "Money Order", 65.00m)
            };
            var seededProducts = productSeeds.Select((seed, index) => new Product(
                Guid.Parse($"20000000-0000-0000-0000-{index + 1:000000000000}"),
                seed.Sku, seed.Name, seed.Description, seed.Category, seed.Price, isActive: true));
            await productRepository.SaveAllAsync(seededProducts, cancellationToken);
        }

        var zones = await shippingZoneRepository.GetAllAsync(cancellationToken);
        if (zones.Count == 0)
        {
            var zoneSeeds = new (string Code, string Name, string Destination)[]
            {
                ("DOM-LOCAL", "Local Metro", "Same city or metropolitan area"),
                ("DOM-STATE", "Intra-State", "Destinations within the same state"),
                ("DOM-NORTH", "North Zone", "Northern India"),
                ("DOM-SOUTH", "South Zone", "Southern India"),
                ("DOM-EAST", "East Zone", "Eastern India"),
                ("DOM-WEST", "West Zone", "Western India"),
                ("DOM-CENTRAL", "Central Zone", "Central India"),
                ("DOM-NE", "North-East Zone", "North-Eastern India"),
                ("DOM-REMOTE", "Remote Areas", "Remote and difficult-to-reach areas"),
                ("INT-REG", "International Regional", "International destinations")
            };
            zones = zoneSeeds.Select((seed, index) => new ShippingZone(
                Guid.Parse($"30000000-0000-0000-0000-{index + 1:000000000000}"),
                seed.Code, seed.Name, seed.Destination, isActive: true)).ToArray();
            await shippingZoneRepository.SaveAllAsync(zones, cancellationToken);
        }

        var rates = await shippingRateRepository.GetAllAsync(cancellationToken);
        if (rates.Count == 0)
        {
            var seededRates = zones.SelectMany((zone, index) => new[]
            {
                new ShippingRate(
                    Guid.Parse($"40000000-0000-0000-0000-{index + 1:000000000000}"),
                    zone.Id,
                    ShippingServiceLevel.Standard,
                    35m + index * 5m,
                    12m + index * 2m,
                    30m,
                    isActive: true),
                new ShippingRate(
                    Guid.Parse($"50000000-0000-0000-0000-{index + 1:000000000000}"),
                    zone.Id,
                    ShippingServiceLevel.Express,
                    65m + index * 7m,
                    22m + index * 3m,
                    30m,
                    isActive: true)
            });
            await shippingRateRepository.SaveAllAsync(seededRates, cancellationToken);
        }

        var staff = await staffRepository.GetAllAsync(cancellationToken);
        if (staff.Count != 0)
        {
            return;
        }

        var seededStaff = new List<StaffMember>();
        var branchIds = branches.Select(branch => branch.Id).ToArray();

        for (var branchIndex = 0; branchIndex < branchIds.Length; branchIndex++)
        {
            var branchNumber = branchIndex + 1;
            seededStaff.Add(CreateStaff(
                $"manager{branchNumber}@apexpost.local",
                $"Branch Manager {branchNumber}",
                StaffRole.BranchManager,
                branchIds[branchIndex],
                passwordService,
                demoPassword));

            for (var clerkIndex = 1; clerkIndex <= 2; clerkIndex++)
            {
                seededStaff.Add(CreateStaff(
                    $"clerk{branchNumber}{clerkIndex}@apexpost.local",
                    $"Counter Clerk {branchNumber}-{clerkIndex}",
                    StaffRole.CounterClerk,
                    branchIds[branchIndex],
                    passwordService,
                    demoPassword));
            }
        }

        seededStaff.Add(CreateStaff(
            "clerk-extra1@apexpost.local",
            "Counter Clerk Extra 1",
            StaffRole.CounterClerk,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "clerk-extra2@apexpost.local",
            "Counter Clerk Extra 2",
            StaffRole.CounterClerk,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "clerk-extra3@apexpost.local",
            "Counter Clerk Extra 3",
            StaffRole.CounterClerk,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "auditor1@apexpost.local",
            "ApexPost Auditor",
            StaffRole.Auditor,
            branchIds[0],
            passwordService,
            demoPassword));
        seededStaff.Add(CreateStaff(
            "admin@apexpost.local",
            "ApexPost Administrator",
            StaffRole.Administrator,
            null,
            passwordService,
            demoPassword));

        await staffRepository.SaveAllAsync(seededStaff, cancellationToken);
    }

    private static StaffMember CreateStaff(
        string email,
        string fullName,
        StaffRole role,
        Guid? branchId,
        IStaffPasswordService passwordService,
        string password)
    {
        var staffId = GuidFromEmail(email);
        return new StaffMember(
            staffId,
            email,
            fullName,
            role,
            branchId,
            isActive: true,
            passwordService.Hash(password));
    }

    private static Guid GuidFromEmail(string email)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

