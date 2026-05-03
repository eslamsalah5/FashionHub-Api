using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingCustomerRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Task 5.2: Pre-migration analysis queries
            // Log the count of AppUsers with UserType=Customer but no Customer record
            migrationBuilder.Sql(@"
                -- Pre-migration analysis: Find orphaned AppUsers
                DECLARE @OrphanedAppUsersCount INT;
                SELECT @OrphanedAppUsersCount = COUNT(*)
                FROM AspNetUsers au
                LEFT JOIN Customers c ON au.Id = c.Id
                WHERE au.UserType = 1  -- UserType.Customer enum value
                  AND c.Id IS NULL;
                
                PRINT 'Pre-migration analysis: Found ' + CAST(@OrphanedAppUsersCount AS VARCHAR(10)) + ' orphaned AppUser records without Customer records';
            ");

            // Log the count of Carts with invalid CustomerId references
            migrationBuilder.Sql(@"
                -- Pre-migration analysis: Find Carts with invalid CustomerId references
                DECLARE @InvalidCartsCount INT;
                SELECT @InvalidCartsCount = COUNT(*)
                FROM Carts ca
                LEFT JOIN Customers c ON ca.CustomerId = c.Id
                WHERE c.Id IS NULL;
                
                PRINT 'Pre-migration analysis: Found ' + CAST(@InvalidCartsCount AS VARCHAR(10)) + ' Cart records with invalid CustomerId references';
            ");

            // Task 5.1: Create migration script to repair missing Customer records
            // Create Customer records for orphaned AppUsers
            migrationBuilder.Sql(@"
                -- Repair: Create missing Customer records
                INSERT INTO Customers (Id)
                SELECT au.Id
                FROM AspNetUsers au
                LEFT JOIN Customers c ON au.Id = c.Id
                WHERE au.UserType = 1  -- UserType.Customer
                  AND c.Id IS NULL;
                
                PRINT 'Migration: Created missing Customer records for orphaned AppUsers';
            ");

            // Task 5.3: Post-migration verification queries
            // Verify all Customer-type AppUsers have Customer records
            migrationBuilder.Sql(@"
                -- Post-migration verification: Check for remaining orphaned AppUsers
                DECLARE @RemainingOrphanedCount INT;
                SELECT @RemainingOrphanedCount = COUNT(*)
                FROM AspNetUsers au
                LEFT JOIN Customers c ON au.Id = c.Id
                WHERE au.UserType = 1
                  AND c.Id IS NULL;
                
                IF @RemainingOrphanedCount = 0
                    PRINT 'Post-migration verification: SUCCESS - All Customer-type AppUsers now have Customer records';
                ELSE
                    PRINT 'Post-migration verification: WARNING - Still found ' + CAST(@RemainingOrphanedCount AS VARCHAR(10)) + ' orphaned AppUser records';
            ");

            // Verify all Carts reference valid Customers
            migrationBuilder.Sql(@"
                -- Post-migration verification: Check for Carts with invalid CustomerId
                DECLARE @InvalidCartsAfterMigration INT;
                SELECT @InvalidCartsAfterMigration = COUNT(*)
                FROM Carts ca
                LEFT JOIN Customers c ON ca.CustomerId = c.Id
                WHERE c.Id IS NULL;
                
                IF @InvalidCartsAfterMigration = 0
                    PRINT 'Post-migration verification: SUCCESS - All Carts reference valid Customers';
                ELSE
                    PRINT 'Post-migration verification: WARNING - Still found ' + CAST(@InvalidCartsAfterMigration AS VARCHAR(10)) + ' Carts with invalid CustomerId references';
            ");

            // Log migration completion
            migrationBuilder.Sql(@"
                PRINT 'Migration RepairMissingCustomerRecords completed successfully';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration repairs data integrity, so Down is not applicable
            // Removing Customer records could break existing carts and would be destructive
            // If rollback is needed, it should be done manually with careful consideration
            migrationBuilder.Sql(@"
                PRINT 'WARNING: RepairMissingCustomerRecords migration does not support automatic rollback';
                PRINT 'Removing Customer records created by this migration could break existing cart functionality';
                PRINT 'If rollback is required, please perform manual data cleanup with caution';
            ");
        }
    }
}
