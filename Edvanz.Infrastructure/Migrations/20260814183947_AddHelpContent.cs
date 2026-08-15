using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpFaqItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Persona = table.Column<int>(type: "int", nullable: false),
                    ModuleKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    QuestionEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    QuestionAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    AnswerEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AnswerAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpFaqItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpModules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Persona = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpArticles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HelpModuleId = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpArticles_HelpModules_HelpModuleId",
                        column: x => x.HelpModuleId,
                        principalTable: "HelpModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HelpTourSteps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HelpModuleId = table.Column<long>(type: "bigint", nullable: false),
                    AnchorKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpTourSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpTourSteps_HelpModules_HelpModuleId",
                        column: x => x.HelpModuleId,
                        principalTable: "HelpModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HelpArticleSections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HelpArticleId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    HeadingEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HeadingAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BodyEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreateAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticleSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HelpArticleSections_HelpArticles_HelpArticleId",
                        column: x => x.HelpArticleId,
                        principalTable: "HelpArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "HelpFaqItems",
                columns: new[] { "Id", "AnswerAr", "AnswerEn", "CreateAt", "DisplayOrder", "IsActive", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[,]
                {
                    { 1L, "افتح طلبات الربط، بص على الطالب المقترح من الكشف، وبعدين اقبل. لو فيه طالب مقترح، استخدم 'اقبل واربط' عشان توصل وتربط في خطوة واحدة.", "Open Link Requests, review the suggested roster match, then Accept. If a match is suggested, use 'Accept & link' to connect and link in one step.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "student_links", 1, "طالب بعتلي طلب ربط. أعمل إيه؟", "A student sent me a link request. What do I do?" },
                    { 2L, "القبول بيوصّل الحساب بس. لازم كمان تربطه بسجل طالب: افتح الطالب واختار 'اربط بسجل الطالب'، وبعدين اختاره بكود الكشف بتاعه.", "Accepting only connects the account. You must also link it to a student record: open the student and choose 'Link to student record', then pick them by their roster code.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "student_links", 1, "قبلت طالب بس لسه مش شايف حاجة.", "I accepted a student but they still see nothing." },
                    { 3L, "طلبك 'قيد الانتظار' لحد ما مدرسك يوافق. مفيش حاجة تانية عليك — استنى الموافقة.", "Your request is 'Pending' until your teacher approves it. There's nothing else to do on your side — wait for the approval.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "linking", 2, "بعت طلب بس مفيش حاجة حصلت.", "I sent a request but nothing happened." },
                    { 4L, "انت في حالة 'بانتظار الربط': موصول بس لسه مش مربوط بسجلك. اطلب من مدرسك يربطك بسجلك.", "You're in 'Awaiting link': connected but not yet linked to your student record. Ask your teacher to link you to your record.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "linking", 2, "مدرسي وافق عليّا بس لسه مش شايف حاجة.", "My teacher approved me but I still see nothing." },
                    { 5L, "مدرسك يقدر يخفي الحضور أو المدفوعات أو الواجب أو الامتحانات. القسم المقفول (أو اللي مش ظاهر) معناه إنه متقفل للطلاب — مش عطل.", "Your teacher can hide Attendance, Payments, Homework or Exams. A locked tile (or a section that doesn't appear) means it's turned off for students — not a bug.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, "linking", 2, "ليه فيه قسم مقفول أو مش موجود؟", "Why is a section locked or missing?" }
                });

            migrationBuilder.InsertData(
                table: "HelpModules",
                columns: new[] { "Id", "CreateAt", "DisplayOrder", "IsActive", "Key", "Persona", "Status", "TitleAr", "TitleEn" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "student_links", 1, 1, "ربط الطلاب", "Student links" },
                    { 2L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "linking", 2, 1, "الربط بمدرس", "Linking to a teacher" }
                });

            migrationBuilder.InsertData(
                table: "HelpArticles",
                columns: new[] { "Id", "CreateAt", "DisplayOrder", "HelpModuleId", "Key", "TitleAr", "TitleEn" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 1L, "connect_vs_bind", "الوصل مقابل الربط", "Connect vs Link" },
                    { 2L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 1L, "link_statuses", "كل حالة معناها إيه", "What each status means" },
                    { 3L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 2L, "why_cant_i_see", "ليه مش شايف بيانات مدرسي؟", "Why can't I see my teacher's data?" },
                    { 4L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 2L, "the_two_codes", "شرح الكودين", "The two codes explained" }
                });

            migrationBuilder.InsertData(
                table: "HelpTourSteps",
                columns: new[] { "Id", "AnchorKey", "BodyAr", "BodyEn", "CreateAt", "DisplayOrder", "HelpModuleId", "TitleAr", "TitleEn" },
                values: new object[,]
                {
                    { 1L, "sl_my_code", "اشير الكود ده اللي من ٨ أرقام لطلابك. لما يكتبوه بيبعتولك طلب ربط.", "Share this 8-digit code with your students. They enter it to send you a link request.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 1L, "كود المدرس بتاعك", "Your teacher code" },
                    { 2L, "sl_requests", "طلبات الطلاب بتوصل هنا. كل طلب بيقترحلك طالب من الكشف لو كود الطالب متطابق مع كود عندك.", "Requests from students arrive here. Each one suggests a roster match when the student's code matches one of yours.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 1L, "طلبات الربط", "Link requests" },
                    { 3L, "sl_accept", "القبول بيوصّل حساب الطالب بس. لسه لازم تربط الحساب ده بسجل طالب عشان أي بيانات توصله.", "Accepting only connects the student's account. You still have to link that account to a student record before any data reaches them.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 1L, "القبول مش معناه الربط", "Accept ≠ linked" },
                    { 4L, "sl_bind", "اختار الطالب من الكشف بالكود بتاعه (مثلاً A12) أو من القايمة. بعد الربط بس الطالب يبدأ يشوف الحضور والمدفوعات وباقي الحاجات.", "Pick the roster student by their code (e.g. A12) or from the list. Only after linking does the student see attendance, payments and the rest.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 4, 1L, "اربطه بسجل الطالب", "Link to a student record" },
                    { 5L, "lk_add_teacher", "دوس ضيف مدرس، وبعدين اكتب كود المدرس بالأرقام واسمك. سيب خانة كود الطالب فاضية لو المدرس مداكش كود.", "Tap Add teacher, then enter your teacher's numeric code and your name. Leave the student code empty if the teacher didn't give you one.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 2L, "ضيف مدرسك", "Add your teacher" },
                    { 6L, "lk_status", "بعد ما تبعت الطلب حالته بتبقى 'قيد الانتظار'. لازم مدرسك يوافق — مجرد إرسال الطلب مبيربطكش.", "After you send the request its status is 'Pending'. Your teacher has to approve it — sending the request alone does not link you.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 2L, "استنى الموافقة", "Wait for approval" },
                    { 7L, "lk_awaiting", "'بانتظار الربط' معناها إن مدرسك وافق على حسابك بس لسه مربطكش بسجلك — علشان كده لسه مش شايف حاجة. اطلب من مدرسك يربطك.", "'Awaiting link' means your teacher approved your account but hasn't linked you to your record yet — so you still see nothing. Ask your teacher to link you.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 2L, "الوصل لوحده مش كفاية", "Connected isn't enough" },
                    { 8L, "lk_locked", "أي قسم رمادي أو مكتوب عليه 'مخفي بواسطة المدرس' معناه إن المدرس قافل القسم ده للطلاب. دي مش مشكلة في التطبيق.", "A greyed or 'hidden by teacher' tile means the teacher turned that section off for students. It is not a bug.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 4, 2L, "الأقسام المخفية", "Hidden modules" }
                });

            migrationBuilder.InsertData(
                table: "HelpArticleSections",
                columns: new[] { "Id", "BodyAr", "BodyEn", "CreateAt", "DisplayOrder", "HeadingAr", "HeadingEn", "HelpArticleId" },
                values: new object[,]
                {
                    { 1L, "الوصل (القبول) بيوافق على حساب الطالب في التطبيق. الربط بيوصّل الحساب ده بسجل طالب معيّن في كشفك. ممكن الطالب يكون موصول بس مش مربوط — وساعتها مبيشوفش أي حاجة.", "Connecting (Accept) approves the student's app account. Linking (Bind) attaches that account to a specific student record on your roster. A student can be connected but not linked — in that state they see nothing.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, "خطوتين منفصلتين", "Two separate steps", 1L },
                    { 2L, "من الطلب الموصول أو من طلابي، اختار 'اربط بسجل الطالب' واختار الطالب بكود الكشف بتاعه (مثلاً A12) — مش كود الحساب اللي من ١٠ حروف.", "From a connected request or from My Students, choose 'Link to student record' and pick the student by their roster code (e.g. A12) — not their 10-character account code.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "إزاي تربط", "How to link", 1L },
                    { 3L, "فك الربط بيوقف وصول الطالب بس بيسيب الحساب موصول، فتقدر تربطه تاني بعدين من غير طلب جديد.", "Unlinking a student stops their access but keeps the account connected, so you can re-link later without a new request.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, "فك الربط بيسيبه موصول", "Unlink keeps them connected", 1L },
                    { 4L, "نشط = موصول ومربوط (وصول كامل). قيد الانتظار = طلب مستني قرارك. بانتظار الربط = اتقبل بس لسه مش مربوط بسجل. مرفوض / متشال = انت أنهيته.", "Active = connected and linked (full access). Pending = a request waiting for your decision. Awaiting link = accepted but not yet linked to a record. Declined / Removed = ended by you.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 2L },
                    { 5L, "بس المدرس اللي حالته 'نشط' (مربوط) بيفتحلك المحتوى. 'قيد الانتظار' مستني الموافقة؛ و'بانتظار الربط' معناها إنك موصول بس لسه مش مربوط.", "Only an 'Active' (linked) teacher opens content. 'Pending' waits for approval; 'Awaiting link' means you're connected but not linked yet.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, "شوف حالتك", "Check your status", 3L },
                    { 6L, "حتى لو مربوط، المدرس ممكن يخفي الحضور أو المدفوعات أو الواجب أو الامتحانات. الأقسام المخفية بتظهر مقفولة أو مبتظهرش خالص.", "Even when linked, a teacher can hide Attendance, Payments, Homework or Exams. Hidden sections show a locked tile or don't appear at all.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "يمكن المدرس أخفاها", "The teacher may have hidden it", 3L },
                    { 7L, "'الكود بتاعي' بيعرّفك وانت بتربط بمدرس. أما كود الحضور (QR) فحاجة تانية — ده اللي مدرسك بيمسحه عشان يسجّلك حاضر في الحصة. متخلطش بينهم.", "'My code' identifies you when linking to a teacher. Your attendance QR is different — it's the code your teacher scans to mark you present in class. Don't confuse the two.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 4L }
                });

            migrationBuilder.CreateIndex(
                name: "UX_HelpArticles_Module_Key",
                table: "HelpArticles",
                columns: new[] { "HelpModuleId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticleSections_Article_Order",
                table: "HelpArticleSections",
                columns: new[] { "HelpArticleId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpFaqItems_Persona_Active_Order",
                table: "HelpFaqItems",
                columns: new[] { "Persona", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HelpModules_Persona_Active_Order",
                table: "HelpModules",
                columns: new[] { "Persona", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_HelpModules_Persona_Key",
                table: "HelpModules",
                columns: new[] { "Persona", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelpTourSteps_Module_Order",
                table: "HelpTourSteps",
                columns: new[] { "HelpModuleId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpArticleSections");

            migrationBuilder.DropTable(
                name: "HelpFaqItems");

            migrationBuilder.DropTable(
                name: "HelpTourSteps");

            migrationBuilder.DropTable(
                name: "HelpArticles");

            migrationBuilder.DropTable(
                name: "HelpModules");
        }
    }
}
