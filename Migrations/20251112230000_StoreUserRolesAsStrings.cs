using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopApI.Migrations
{
    public partial class StoreUserRolesAsStrings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Users` MODIFY `Role` varchar(50) NOT NULL;");
            migrationBuilder.Sql(@"UPDATE `Users` SET `Role` = CASE `Role`
                WHEN '0' THEN 'Admin'
                WHEN '1' THEN 'Manager'
                WHEN '2' THEN 'Support'
                WHEN '3' THEN 'Customer'
                ELSE `Role`
            END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE `Users` SET `Role` = CASE `Role`
                WHEN 'Admin' THEN '0'
                WHEN 'Manager' THEN '1'
                WHEN 'Support' THEN '2'
                WHEN 'Customer' THEN '3'
                ELSE '0'
            END;");
            migrationBuilder.Sql("ALTER TABLE `Users` MODIFY `Role` int NOT NULL;");
        }
    }
}
