using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EyesofterWeb.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asp_net_roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    accion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    entidad_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    detalle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    origen = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    exito = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clinicas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    telefono = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    correo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    codigo_postal = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    latitud = table.Column<double>(type: "double precision", nullable: true),
                    longitud = table.Column<double>(type: "double precision", nullable: true),
                    horario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    es_asociada = table.Column<bool>(type: "boolean", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinicas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consentimientos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    version_aviso = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aceptado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    medio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consentimientos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispositivos",
                columns: table => new
                {
                    token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    plataforma = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    registrado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispositivos", x => x.token);
                });

            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    datos = table.Column<string>(type: "jsonb", nullable: false),
                    creada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    leida_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recordatorios = table.Column<int>(type: "integer", nullable: false),
                    ultimo_aviso_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    pendiente_envio = table.Column<bool>(type: "boolean", nullable: false),
                    envio_es_recordatorio = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificaciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "planteles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planteles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reportes_falla",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    correo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    plataforma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    respuesta = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reportes_falla", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tutores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    telefono = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    correo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    parentesco = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "asp_net_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entregas_notificacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    notificacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    exito = table.Column<bool>(type: "boolean", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    detalle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entregas_notificacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_entregas_notificacion_notificaciones_notificacion_id",
                        column: x => x.notificacion_id,
                        principalTable: "notificaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campanas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ciclo_escolar = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    fecha_inicio = table.Column<DateTime>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateTime>(type: "date", nullable: true),
                    plantel_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campanas", x => x.id);
                    table.ForeignKey(
                        name: "fk_campanas_planteles_plantel_id",
                        column: x => x.plantel_id,
                        principalTable: "planteles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alumnos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    matricula = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fecha_nacimiento = table.Column<DateTime>(type: "date", nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    grado = table.Column<int>(type: "integer", nullable: false),
                    grupo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    usa_lentes = table.Column<bool>(type: "boolean", nullable: false),
                    tutor_id = table.Column<int>(type: "integer", nullable: false),
                    plantel_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alumnos", x => x.id);
                    table.ForeignKey(
                        name: "fk_alumnos_planteles_plantel_id",
                        column: x => x.plantel_id,
                        principalTable: "planteles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alumnos_tutores_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tutor_id = table.Column<int>(type: "integer", nullable: true),
                    alumno_id = table.Column<int>(type: "integer", nullable: true),
                    clinica_id = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_users_alumnos_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumnos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_asp_net_users_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_asp_net_users_tutores_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tamizajes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    folio = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    alumno_id = table.Column<int>(type: "integer", nullable: false),
                    campana_id = table.Column<int>(type: "integer", nullable: true),
                    fecha = table.Column<DateTime>(type: "date", nullable: false),
                    semaforo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    agudeza_ojo_derecho = table.Column<string>(type: "text", nullable: true),
                    agudeza_ojo_izquierdo = table.Column<string>(type: "text", nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recibido_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tamizajes", x => x.id);
                    table.ForeignKey(
                        name: "fk_tamizajes_alumnos_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumnos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tamizajes_campanas_campana_id",
                        column: x => x.campana_id,
                        principalTable: "campanas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_roles",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "asp_net_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_tokens",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "casos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    folio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alumno_id = table.Column<int>(type: "integer", nullable: false),
                    clinica_id = table.Column<int>(type: "integer", nullable: true),
                    semaforo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_deteccion = table.Column<DateTime>(type: "date", nullable: false),
                    agudeza_ojo_derecho = table.Column<string>(type: "text", nullable: false),
                    agudeza_ojo_izquierdo = table.Column<string>(type: "text", nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    fecha_cierre = table.Column<DateTime>(type: "date", nullable: true),
                    registrado_por = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tamizaje_id = table.Column<int>(type: "integer", nullable: true),
                    clinica_destino_manual = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    fecha_atencion = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_casos", x => x.id);
                    table.ForeignKey(
                        name: "fk_casos_alumnos_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumnos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_casos_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_casos_tamizajes_tamizaje_id",
                        column: x => x.tamizaje_id,
                        principalTable: "tamizajes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "citas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    clinica_id = table.Column<int>(type: "integer", nullable: true),
                    fecha_preferida = table.Column<DateTime>(type: "date", nullable: false),
                    horario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    comentario = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    solicitada_por_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    creada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_citas", x => x.id);
                    table.ForeignKey(
                        name: "fk_citas_casos_caso_id",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_citas_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comprobantes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    subido_por_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tamano_bytes = table.Column<int>(type: "integer", nullable: false),
                    contenido = table.Column<byte[]>(type: "bytea", nullable: false),
                    subido_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    revisado_por = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    revisado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comprobantes", x => x.id);
                    table.ForeignKey(
                        name: "fk_comprobantes_casos_caso_id",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eventos_caso",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha = table.Column<DateTime>(type: "date", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    registrado_por = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_caso", x => x.id);
                    table.ForeignKey(
                        name: "fk_eventos_caso_casos_caso_id",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    clinica_id = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    usado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usado_por = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pases", x => x.id);
                    table.ForeignKey(
                        name: "fk_pases_casos_caso_id",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pases_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "atenciones",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caso_id = table.Column<int>(type: "integer", nullable: false),
                    clinica_id = table.Column<int>(type: "integer", nullable: true),
                    clinica_nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    fecha = table.Column<DateTime>(type: "date", nullable: false),
                    valoracion = table.Column<string>(type: "text", nullable: false),
                    indicaciones = table.Column<string>(type: "text", nullable: true),
                    correccion_optica = table.Column<bool>(type: "boolean", nullable: false),
                    origen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    pase_id = table.Column<int>(type: "integer", nullable: true),
                    comprobante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registrado_por = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    registrado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atenciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_atenciones_casos_caso_id",
                        column: x => x.caso_id,
                        principalTable: "casos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_atenciones_clinicas_clinica_id",
                        column: x => x.clinica_id,
                        principalTable: "clinicas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_atenciones_comprobantes_comprobante_id",
                        column: x => x.comprobante_id,
                        principalTable: "comprobantes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_atenciones_pases_pase_id",
                        column: x => x.pase_id,
                        principalTable: "pases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "planteles",
                columns: new[] { "id", "activo", "clave", "nombre" },
                values: new object[] { 1, true, null, "Escuela Primaria-Secundaria METANOIA" });

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_matricula",
                table: "alumnos",
                column: "matricula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_nivel_grado_grupo",
                table: "alumnos",
                columns: new[] { "nivel", "grado", "grupo" });

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_plantel_id",
                table: "alumnos",
                column: "plantel_id");

            migrationBuilder.CreateIndex(
                name: "ix_alumnos_tutor_id",
                table: "alumnos",
                column: "tutor_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "asp_net_role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "asp_net_roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "asp_net_user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "asp_net_user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "asp_net_user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "asp_net_users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_alumno_id",
                table: "asp_net_users",
                column: "alumno_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_clinica_id",
                table: "asp_net_users",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_tutor_id",
                table: "asp_net_users",
                column: "tutor_id");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "asp_net_users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_atenciones_caso_id",
                table: "atenciones",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_atenciones_clinica_id",
                table: "atenciones",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_atenciones_comprobante_id",
                table: "atenciones",
                column: "comprobante_id");

            migrationBuilder.CreateIndex(
                name: "ix_atenciones_pase_id",
                table: "atenciones",
                column: "pase_id");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_entidad_entidad_id",
                table: "auditoria",
                columns: new[] { "entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_fecha",
                table: "auditoria",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "ix_campanas_plantel_id",
                table: "campanas",
                column: "plantel_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_alumno_id",
                table: "casos",
                column: "alumno_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_clinica_id",
                table: "casos",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_casos_estado",
                table: "casos",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_casos_folio",
                table: "casos",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_casos_tamizaje_id",
                table: "casos",
                column: "tamizaje_id");

            migrationBuilder.CreateIndex(
                name: "ix_citas_caso_id",
                table: "citas",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_citas_clinica_id",
                table: "citas",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_comprobantes_caso_id",
                table: "comprobantes",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_comprobantes_estado",
                table: "comprobantes",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_consentimientos_usuario_id_version_aviso",
                table: "consentimientos",
                columns: new[] { "usuario_id", "version_aviso" });

            migrationBuilder.CreateIndex(
                name: "ix_dispositivos_usuario_id",
                table: "dispositivos",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_entregas_notificacion_notificacion_id",
                table: "entregas_notificacion",
                column: "notificacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventos_caso_caso_id",
                table: "eventos_caso",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_notificaciones_pendiente_envio",
                table: "notificaciones",
                column: "pendiente_envio",
                filter: "pendiente_envio");

            migrationBuilder.CreateIndex(
                name: "ix_notificaciones_usuario_id_creada_en",
                table: "notificaciones",
                columns: new[] { "usuario_id", "creada_en" });

            migrationBuilder.CreateIndex(
                name: "ix_pases_caso_id",
                table: "pases",
                column: "caso_id");

            migrationBuilder.CreateIndex(
                name: "ix_pases_clinica_id",
                table: "pases",
                column: "clinica_id");

            migrationBuilder.CreateIndex(
                name: "ix_pases_codigo",
                table: "pases",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tamizajes_alumno_id",
                table: "tamizajes",
                column: "alumno_id");

            migrationBuilder.CreateIndex(
                name: "ix_tamizajes_campana_id",
                table: "tamizajes",
                column: "campana_id");

            migrationBuilder.CreateIndex(
                name: "ix_tamizajes_folio",
                table: "tamizajes",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tutores_telefono",
                table: "tutores",
                column: "telefono");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asp_net_role_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_logins");

            migrationBuilder.DropTable(
                name: "asp_net_user_roles");

            migrationBuilder.DropTable(
                name: "asp_net_user_tokens");

            migrationBuilder.DropTable(
                name: "atenciones");

            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "citas");

            migrationBuilder.DropTable(
                name: "consentimientos");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "dispositivos");

            migrationBuilder.DropTable(
                name: "entregas_notificacion");

            migrationBuilder.DropTable(
                name: "eventos_caso");

            migrationBuilder.DropTable(
                name: "reportes_falla");

            migrationBuilder.DropTable(
                name: "asp_net_roles");

            migrationBuilder.DropTable(
                name: "asp_net_users");

            migrationBuilder.DropTable(
                name: "comprobantes");

            migrationBuilder.DropTable(
                name: "pases");

            migrationBuilder.DropTable(
                name: "notificaciones");

            migrationBuilder.DropTable(
                name: "casos");

            migrationBuilder.DropTable(
                name: "clinicas");

            migrationBuilder.DropTable(
                name: "tamizajes");

            migrationBuilder.DropTable(
                name: "alumnos");

            migrationBuilder.DropTable(
                name: "campanas");

            migrationBuilder.DropTable(
                name: "tutores");

            migrationBuilder.DropTable(
                name: "planteles");
        }
    }
}
