using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EyesofterWeb.Migrations
{
    /// <summary>
    /// Grupos escolares: tabla "grupos" y, en "alumnos", grupo_escolar_id y egresado.
    /// Crea un grupo por cada combinación escuela/nivel/grado/letra que ya tenían los alumnos
    /// y los inscribe en él, para que "pasar de año" funcione desde el primer día.
    /// </summary>
    public partial class GruposEscolares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "egresado",
                table: "alumnos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "grupo_escolar_id",
                table: "alumnos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "grupos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantel_id = table.Column<int>(type: "integer", nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    grado = table.Column<int>(type: "integer", nullable: false),
                    letra = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    ciclo_escolar = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    docente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    promovido_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos", x => x.id);
                    table.ForeignKey(
                        name: "fk_grupos_planteles_plantel_id",
                        column: x => x.plantel_id,
                        principalTable: "planteles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_grupo_escolar_id",
                table: "alumnos",
                column: "grupo_escolar_id");

            migrationBuilder.CreateIndex(
                name: "ix_grupos_plantel_id_nivel_grado_letra",
                table: "grupos",
                columns: new[] { "plantel_id", "nivel", "grado", "letra" },
                unique: true,
                filter: "activo");

            migrationBuilder.AddForeignKey(
                name: "fk_alumnos_grupos_escolares_grupo_escolar_id",
                table: "alumnos",
                column: "grupo_escolar_id",
                principalTable: "grupos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Datos existentes: un grupo por cada escuela/nivel/grado/letra ya capturados en los alumnos.
            // El ciclo escolar empieza en agosto.
            migrationBuilder.Sql(@"
INSERT INTO grupos (plantel_id, nivel, grado, letra, ciclo_escolar, activo)
SELECT DISTINCT a.plantel_id, a.nivel, a.grado, a.grupo,
       CASE WHEN EXTRACT(MONTH FROM CURRENT_DATE) >= 8
            THEN EXTRACT(YEAR FROM CURRENT_DATE)::int || '-' || (EXTRACT(YEAR FROM CURRENT_DATE)::int + 1)
            ELSE (EXTRACT(YEAR FROM CURRENT_DATE)::int - 1) || '-' || EXTRACT(YEAR FROM CURRENT_DATE)::int
       END,
       TRUE
FROM alumnos a;

UPDATE alumnos a
SET grupo_escolar_id = g.id
FROM grupos g
WHERE g.activo AND g.plantel_id = a.plantel_id AND g.nivel = a.nivel AND g.grado = a.grado AND g.letra = a.grupo;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_alumnos_grupos_escolares_grupo_escolar_id",
                table: "alumnos");

            migrationBuilder.DropTable(
                name: "grupos");

            migrationBuilder.DropIndex(
                name: "ix_alumnos_grupo_escolar_id",
                table: "alumnos");

            migrationBuilder.DropColumn(
                name: "egresado",
                table: "alumnos");

            migrationBuilder.DropColumn(
                name: "grupo_escolar_id",
                table: "alumnos");
        }
    }
}
