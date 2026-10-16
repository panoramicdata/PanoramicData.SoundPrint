using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PanoramicData.SoundPrint.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "store_info",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    parameter_fingerprint = table.Column<long>(type: "bigint", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_info", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    idx = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    track_id = table.Column<string>(type: "text", nullable: false),
                    landmark_count = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.idx);
                });

            migrationBuilder.CreateTable(
                name: "fingerprints",
                columns: table => new
                {
                    hash = table.Column<int>(type: "integer", nullable: false),
                    track = table.Column<int>(type: "integer", nullable: false),
                    frame = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "FK_fingerprints_tracks_track",
                        column: x => x.track,
                        principalTable: "tracks",
                        principalColumn: "idx",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fingerprints_hash",
                table: "fingerprints",
                column: "hash")
                .Annotation("Npgsql:IndexInclude", new[] { "track", "frame" });

            migrationBuilder.CreateIndex(
                name: "ix_fingerprints_track",
                table: "fingerprints",
                column: "track");

            migrationBuilder.CreateIndex(
                name: "ux_tracks_track_id",
                table: "tracks",
                column: "track_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fingerprints");

            migrationBuilder.DropTable(
                name: "store_info");

            migrationBuilder.DropTable(
                name: "tracks");
        }
    }
}
