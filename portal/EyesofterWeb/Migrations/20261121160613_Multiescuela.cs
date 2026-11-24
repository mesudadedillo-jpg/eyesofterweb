using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EyesofterWeb.Migrations
{
    /// <inheritdoc />
    public partial class Multiescuela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_alumnos_matricula",
                table: "alumnos");

            migrationBuilder.DropIndex(
                name: "ix_alumnos_plantel_id",
                table: "alumnos");

            migrationBuilder.AddColumn<int>(
                name: "plantel_id",
                table: "tutores",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "plantel_id",
                table: "auditoria",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "administrador_global",
                table: "asp_net_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "convenios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantel_id = table.Column<int>(type: "integer", nullable: false),
                    clinica_id = table.Column<int>(type: "integer", nullable: false),
                    descuento = table.Column<int>(type: "integer", nullable: false),
                    beneficio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vigente_hasta = table.Column<DateTime>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_convenios", x => x.id);
                    table.ForeignKey(
                        name: "fk_convenios_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_convenios_planteles_plantel_id",
                        column: x => x.plantel_id,
                        principalTable: "planteles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_planteles",
                columns: table => new
                {
                    usuario_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    plantel_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_planteles", x => new { x.usuario_id, x.plantel_id });
                    table.ForeignKey(
                        name: "fk_usuario_planteles_planteles_plantel_id",
                        column: x => x.plantel_id,
                        principalTable: "planteles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuario_planteles_users_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vinculaciones_escritorio",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    codigo = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    equipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    creada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    aprobada_por = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculaciones_escritorio", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tutores_plantel_id",
                table: "tutores",
                column: "plantel_id");

            migrationBuilder.CreateIndex(
                name: "ix_planteles_clave",
                table: "planteles",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_plantel_id",
                table: "auditoria",
                column: "plantel_id");

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_matricula",
                table: "alumnos",
                column: "matricula");

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_plantel_id_matricula",
                table: "alumnos",
                columns: new[] { "plantel_id", "matricula" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_convenios_clinica_id",
                table: "convenios",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_convenios_plantel_id_clinica_id",
                table: "convenios",
                columns: new[] { "plantel_id", "clinica_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_planteles_plantel_id",
                table: "usuario_planteles",
                column: "plantel_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculaciones_escritorio_codigo",
                table: "vinculaciones_escritorio",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculaciones_escritorio_expira_en",
                table: "vinculaciones_escritorio",
                column: "expira_en");

            migrationBuilder.AddForeignKey(
                name: "fk_tutores_planteles_plantel_id",
                table: "tutores",
                column: "plantel_id",
                principalTable: "planteles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Hasta hoy había una sola escuela (METANOIA, id 1): el personal que ya existe queda
            // asignado a ella, igual que la bitácora existente. Así nadie pierde acceso al migrar.
            migrationBuilder.Sql("""
                INSERT INTO usuario_planteles (usuario_id, plantel_id)
                SELECT DISTINCT ur.user_id, 1
                FROM asp_net_user_roles ur
                JOIN asp_net_roles r ON r.id = ur.role_id
                WHERE r.name IN ('Administrador', 'Enfermeria')
                ON CONFLICT DO NOTHING;
                UPDATE auditoria SET plantel_id = 1 WHERE plantel_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tutores_planteles_plantel_id",
                table: "tutores");

            migrationBuilder.DropTable(
                name: "convenios");

            migrationBuilder.DropTable(
                name: "usuario_planteles");

            migrationBuilder.DropTable(
                name: "vinculaciones_escritorio");

            migrationBuilder.DropIndex(
                name: "ix_tutores_plantel_id",
                table: "tutores");

            migrationBuilder.DropIndex(
                name: "ix_planteles_clave",
                table: "planteles");

            migrationBuilder.DropIndex(
                name: "ix_auditoria_plantel_id",
                table: "auditoria");

            migrationBuilder.DropIndex(
                name: "ix_alumnos_matricula",
                table: "alumnos");

            migrationBuilder.DropIndex(
                name: "ix_alumnos_plantel_id_matricula",
                table: "alumnos");

            migrationBuilder.DropColumn(
                name: "plantel_id",
                table: "tutores");

            migrationBuilder.DropColumn(
                name: "plantel_id",
                table: "auditoria");

            migrationBuilder.DropColumn(
                name: "administrador_global",
                table: "asp_net_users");

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_matricula",
                table: "alumnos",
                column: "matricula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_plantel_id",
                table: "alumnos",
                column: "plantel_id");
        }
    }
}
