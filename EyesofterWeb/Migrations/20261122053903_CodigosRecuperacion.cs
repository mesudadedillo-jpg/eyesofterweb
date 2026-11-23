using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EyesofterWeb.Migrations
{
    /// <inheritdoc />
    public partial class CodigosRecuperacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "codigos_recuperacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    codigo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    canal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    vence_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    usado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_codigos_recuperacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_codigos_recuperacion_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_codigos_recuperacion_usuario_id_creado_en",
                table: "codigos_recuperacion",
                columns: new[] { "usuario_id", "creado_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codigos_recuperacion");
        }
    }
}
