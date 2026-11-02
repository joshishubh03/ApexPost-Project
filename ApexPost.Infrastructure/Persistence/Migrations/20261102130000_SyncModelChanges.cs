using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Intentionally empty. This migration was generated while the CK_Branches_OpeningHours
    /// check constraint was temporarily commented out of ApexPostDbContext. The constraint has
    /// since been restored, so dropping it here would leave the database out of sync with the model.
    /// The migration is kept (as a no-op) so databases that already recorded it stay consistent.
    /// </summary>
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
