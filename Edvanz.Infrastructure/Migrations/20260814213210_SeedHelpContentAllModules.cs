using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Edvanz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedHelpContentAllModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "BodyAr", "BodyEn", "HeadingAr", "HeadingEn" },
                values: new object[] { "الشاشة الرئيسية بتوريك يوم واحد. استخدم شريط الأيام عشان تنقل بين الأيام؛ دي مش قايمة بكل حصصك.", "The home screen is scoped to one day. Use the week strip to move between days; it is not a list of all your sessions.", "بتوريك يوم واحد", "Day-scoped" });

            migrationBuilder.UpdateData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "BodyAr", "BodyEn", "HeadingAr", "HeadingEn" },
                values: new object[] { "من حصة اليوم تقدر تدخل على طول تسجّل حضور أو تحصّل مدفوعات.", "From a day's session you can jump straight into taking attendance or collecting payment.", "إجراءات سريعة", "Quick actions" });

            migrationBuilder.UpdateData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "BodyAr", "BodyEn" },
                values: new object[] { "الحصة الشهرية بتتحاسب مرة كل شهر وبتسمح بمتأخرات ودفع مقدّم. الحصة بالحصة بتتحاسب لكل كلاس. اختار ده وانت بتعمل الحصة — بيأثر على كل شاشات الفلوس بعد كده.", "A Monthly session bills once per month and supports arrears/advance. A Per-session session bills per class. Pick this when you create the session — it affects every payment screen afterward." });

            migrationBuilder.UpdateData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "Key", "TitleAr", "TitleEn" },
                values: new object[] { "dashboard_basics", "إزاي تقرا الشاشة الرئيسية", "Reading your home screen" });

          

            migrationBuilder.UpdateData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "AnswerAr", "AnswerEn", "DisplayOrder", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[] { "الحصة كلاس واحد. المجموعة بتجمع كذا حصة مع بعض. الحصة ممكن تكون في مجموعة، و'ربط الحصص' بيخلي الطلاب يحضروا الحصص المربوطة بالتبادل.", "A session is a single class. A group bundles several sessions together. A session can belong to a group, and 'membership link' lets students attend linked sessions interchangeably.", 3, "sessions", 1, "إيه الفرق بين الحصة والمجموعة؟", "What's the difference between a session and a group?" });

            migrationBuilder.UpdateData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "AnswerAr", "AnswerEn", "DisplayOrder", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[] { "الحصص مبترجعش؛ الحذف نهائي. الطلاب بس اللي بيروحوا سلة المحذوفات (١٠ أيام). اعمل الحصة تاني واربط الطلاب بيها.", "Sessions can't be restored; deletion is permanent. Only students go to the recycle bin (for 10 days). Recreate the session and re-assign the students.", 4, "sessions", 1, "حذفت حصة بالغلط — إزاي أرجّعها؟", "I deleted a session by mistake — how do I restore it?" });

            migrationBuilder.UpdateData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "AnswerAr", "AnswerEn", "DisplayOrder", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[] { "ده بيحصل لما اسم الحصة في الملف مش متطابق مع حصصك. الطلاب اتضافوا برضه — اربطهم بحصة بعد كده.", "That happens when the session name in your sheet didn't match one of your sessions. The students are still imported — just assign them to a session afterward.", 5, "students", 1, "بعض الطلاب اللي استوردتهم من غير حصة.", "Some bulk-imported students have no session." });

            migrationBuilder.InsertData(
                table: "HelpFaqItems",
                columns: new[] { "Id", "AnswerAr", "AnswerEn", "CreateAt", "DisplayOrder", "IsActive", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[,]
                {
                    { 6L, "المسح بيحط الطلاب في الطابور بس. لازم تدوس ابعت عشان تسجّل الدفعة. وافتكر إن الطلاب المعلّقين مش بيتسجّلوا عن قصد.", "Scanning only queues students. You must tap Submit to record the batch. Also remember students left on 'Hold' are intentionally not recorded.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 6, true, "attendance", 1, "مسحت طلاب بس مفيش حاجة اتحفظت.", "I scanned students but nothing was saved." },
                    { 7L, "السحب = تاخد كاش من محفظة المساعد (بيصفّرها). الاسترداد = ترجّع فلوس لطالب (قيمة بالسالب على المُحصّل). إجراءين مختلفين تماماً.", "Withdraw = you take cash from an assistant's wallet (resets it to zero). Refund = you give money back to a student (a negative entry against the collector). Different actions entirely.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 7, true, "payments", 1, "إيه الفرق بين السحب والاسترداد؟", "What's the difference between Withdraw and Refund?" },
                    { 8L, "دخل في نص الشهر، فبيتحاسب جزئي على شهره الأول حسب شرائح ١٠/١٠/١٠ في الإعدادات. شاشة النتيجة بتوري علامة 'جزئي'.", "They joined mid-month, so they're prorated for their first month based on the 10/10/10-day tiers in Settings. The result screen shows a 'Prorated' badge.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 8, true, "payments", 1, "ليه الطالب الجديد عليه مبلغ جزئي؟", "Why does a new student owe a partial amount?" },
                    { 9L, "لإنه امتحان داخل الحصة، فالحضور للقراءة بس — بييجي من حضور الحصة. استخدم امتحان في وقت منفصل لو عايز الامتحان يكون ليه حضوره.", "It's a during-session exam, so attendance is read-only — it comes from the class session. Use a separate-time exam if you want the exam to have its own attendance.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 9, true, "offline_exams", 1, "ليه مش عارف أعدّل الحضور في امتحان؟", "Why can't I edit attendance on an exam?" },
                    { 10L, "بص على نطاق استهدافه — ده الجمهور (أنهي حصص/مجموعات تشوفه). الفيديو في وحدة من غير نطاق مش ظاهر لحد. وتأكد إنه منشور، مش مسودة.", "Check its target scope — that's the audience (which sessions/groups can see it). A video in a unit with no scope is visible to no one. Also confirm it's Published, not Draft.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 10, true, "videos", 1, "نشرت فيديو بس الطلاب مش شايفينه.", "I published a video but students can't see it." },
                    { 11L, "داخل التطبيق بيوري كود كل طالب في تطبيقه. المطبوع بيخفي الكود من التطبيق لأنك بتوزّع كروت مطبوعة. اختار اللي يناسب طريقة مسحك للطلاب.", "Soft QR shows each student's code inside their app. Physical QR hides the in-app code because you hand out printed cards. Choose whichever matches how you scan students in.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 11, true, "settings", 1, "إيه الفرق بين QR داخل التطبيق والمطبوع؟", "What's the difference between Soft and Physical QR?" },
                    { 12L, "طلبك 'قيد الانتظار' لحد ما مدرسك يوافق. مفيش حاجة تانية عليك — استنى الموافقة.", "Your request is 'Pending' until your teacher approves it. There's nothing else to do on your side — wait for the approval.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "linking", 2, "بعت طلب بس مفيش حاجة حصلت.", "I sent a request but nothing happened." },
                    { 13L, "انت في حالة 'بانتظار الربط': موصول بس لسه مش مربوط بسجلك. اطلب من مدرسك يربطك بسجلك.", "You're in 'Awaiting link': connected but not yet linked to your student record. Ask your teacher to link you to your record.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "linking", 2, "مدرسي وافق عليّا بس لسه مش شايف حاجة.", "My teacher approved me but I still see nothing." },
                    { 14L, "مدرسك يقدر يخفي الحضور أو المدفوعات أو الواجب أو الامتحانات. القسم المقفول (أو اللي مش ظاهر) معناه إنه متقفل للطلاب — مش عطل.", "Your teacher can hide Attendance, Payments, Homework or Exams. A locked tile (or a section that doesn't appear) means it's turned off for students — not a bug.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, "linking", 2, "ليه فيه قسم مقفول أو مش موجود؟", "Why is a section locked or missing?" },
                    { 15L, "مش هتدفع — شاشة المدفوعات بتتابع بس اللي دفعته واللي عليك. سلّم دفعتك لمدرسك؛ هو بيسجّلها وبتظهر هنا.", "You don't — the payments screen only tracks what you've paid and what's due. Hand your payment to your teacher; they record it and it shows here.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 4, true, "payment", 2, "إزاي أدفع جوه التطبيق؟", "How do I pay inside the app?" },
                    { 16L, "مبلغ بـ −جنيه هو شهر متأخر — اللي لسه عليك. المبالغ اللي دفعتها أو القادمة بتظهر +جنيه. ده مش رسوم زيادة.", "A −LE amount is an overdue month — what you still owe. Amounts you've paid or that are upcoming show as +LE. It's not an extra charge.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 5, true, "payment", 2, "ليه فيه ناقص (−) جنب مبلغ؟", "Why is there a minus (−) next to an amount?" },
                    { 17L, "لسه موصلش ميعاد بدايته — هتشوف 'بيبدأ الساعة …' مع عد تنازلي. ارجع لما الميعاد يفتح. وبمجرد ما تدوس ابدأ، المحاولة المؤقتة بتبدأ ومبترجعش.", "It hasn't reached its start time — you'll see 'starts at …' with a countdown. Come back when the window opens. Once you tap Start, the timed attempt begins and can't be undone.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 6, true, "online_exams", 2, "الامتحان مش سايبني أبدأ لسه.", "The exam won't let me start yet." },
                    { 18L, "محاولتك خلصت، فلازم تعيد قبل ما تسلّم تاني. بعد التسليم، 'إعادة' بتظهر بس لو مدرسك سمح بالإعادة.", "Your attempt is used up, so you must retake before you can submit again. After submitting, a 'Retry' option only appears if your teacher allowed retakes.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 7, true, "videos", 2, "ليه الكويز بيقول إعادة بدل تسليم؟", "Why does the quiz say Retry instead of Submit?" },
                    { 19L, "مفيش — المدرس بس اللي بيسحب الكاش منك. انت بتمسك الكاش وتسلّمه؛ المدرس بيسجّله ومحفظتك بتتصفّر.", "There isn't one — only the teacher withdraws cash from you. You hold the cash and hand it over; the teacher records it and your wallet resets to zero.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "wallet", 3, "فين زر السحب في محفظتي؟", "Where is the Withdraw button on my wallet?" },
                    { 20L, "مدرسك بيتحكم في صلاحياتك، فبعض العناصر ظاهرة بس مقفولة. اطلب من مدرسك يديك الصلاحية اللي محتاجها.", "Your teacher controls your permissions, so some items are visible but blocked. Ask your teacher to grant the permission you need.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "wallet", 3, "عنصر في القايمة بيديني خطأ صلاحية.", "A menu item gives me a permission error." }
                });

            migrationBuilder.UpdateData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 1L,
                column: "DisplayOrder",
                value: 4);

            migrationBuilder.UpdateData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "Key", "Persona", "TitleAr", "TitleEn" },
                values: new object[] { "dashboard", 1, "الشاشة الرئيسية", "Home dashboard" });

            migrationBuilder.InsertData(
                table: "HelpModules",
                columns: new[] { "Id", "CreateAt", "DisplayOrder", "IsActive", "Key", "Persona", "Status", "TitleAr", "TitleEn" },
                values: new object[,]
                {
                    { 3L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "sessions", 1, 1, "الحصص والمجموعات", "Sessions & groups" },
                    { 4L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, "students", 1, 1, "الطلاب", "Students" },
                    { 5L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 5, true, "attendance", 1, 1, "الحضور", "Attendance" },
                    { 6L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 6, true, "payments", 1, 1, "المدفوعات", "Payments" },
                    { 7L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 7, true, "online_exams", 1, 1, "الامتحانات الأونلاين", "Online exams" },
                    { 8L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 8, true, "offline_exams", 1, 1, "الامتحانات الورقية", "Offline exams" },
                    { 9L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 9, true, "videos", 1, 1, "الفيديوهات", "Videos" },
                    { 10L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 10, true, "reports", 1, 1, "التقارير", "Reports" },
                    { 11L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 11, true, "export", 1, 1, "التصدير", "Export" },
                    { 12L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 12, true, "audit_trail", 1, 1, "سجل النشاط", "Audit trail" },
                    { 13L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 13, true, "recycle_bin", 1, 1, "سلة المحذوفات", "Recycle bin" },
                    { 14L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 14, true, "assistants", 1, 1, "إدارة المساعدين", "Assistant management" },
                    { 15L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 15, true, "settings", 1, 1, "الإعدادات", "Settings" },
                    { 16L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "linking", 2, 1, "الربط بمدرس", "Linking to a teacher" },
                    { 17L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "home", 2, 1, "مدرسينك", "Your teachers" },
                    { 18L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, "attendance", 2, 1, "الحضور", "Attendance" },
                    { 19L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 4, true, "payment", 2, 1, "المدفوعات", "Payments" },
                    { 20L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 5, true, "videos", 2, 1, "الفيديوهات", "Videos" },
                    { 21L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 6, true, "online_exams", 2, 1, "الامتحانات الأونلاين", "Online exams" },
                    { 22L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 7, true, "offline_exams", 2, 1, "الامتحانات الورقية", "Offline exams" },
                    { 23L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "wallet", 3, 1, "تحصيلاتك", "Your collections" }
                });

            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "TitleAr", "TitleEn" },
                values: new object[] { "dash_week_strip", "شريط الأيام فوق بيختار اليوم. الكروت اللي تحت بتوريك حصص اليوم ده بس — اليوم الفاضي معناه مفيش حصص فيه.", "The week strip at the top chooses the day. The cards below show only that day's sessions — an empty day just means nothing is scheduled then.", "اختار اليوم", "Pick a day" });
            migrationBuilder.UpdateData(
              table: "HelpArticles",
              keyColumn: "Id",
              keyValue: 4L,
              columns: new[] { "DisplayOrder", "HelpModuleId", "Key", "TitleAr", "TitleEn" },
              values: new object[] { 1, 3L, "monthly_vs_persession", "شهري مقابل بالحصة", "Monthly vs Per-session" });
            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "TitleAr", "TitleEn" },
                values: new object[] { "dash_session_card", "دوس على الحصة عشان تسجّل حضور أو تحصّل فلوس. علامة 'امتحان' معناها إن الحصة دي فيها امتحان النهاردة.", "Tap a session to take attendance or collect payments for it. An 'Exam' badge means that class has an exam today.", "حصص النهاردة", "Today's sessions" });

            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "DisplayOrder", "HelpModuleId", "TitleAr", "TitleEn" },
                values: new object[] { "ses_create", "الحصة هي الكلاس. وانت بتعملها بتختار نوع الدفع — شهري أو بالحصة — وده بيحدد طريقة الفلوس بتاعتها.", "A session is a class. When you create it you choose a payment type — Monthly or Per-session — which drives how billing works for it.", 1, 3L, "اعمل حصة", "Create a session" });

            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 8L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "DisplayOrder", "HelpModuleId", "TitleAr", "TitleEn" },
                values: new object[] { "ses_group", "المجموعة بتجمع كذا حصة مع بعض. استخدم فلتر 'المجموعات بس' عشان تخفي الحصص المفردة.", "A group bundles several sessions together. Use the 'Groups only' filter to hide standalone sessions.", 2, 3L, "المجموعات", "Groups" });

            migrationBuilder.InsertData(
                table: "HelpArticles",
                columns: new[] { "Id", "CreateAt", "DisplayOrder", "HelpModuleId", "Key", "TitleAr", "TitleEn" },
                values: new object[,]
                {
                    { 5L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 3L, "membership_link", "'ربط الحصص' بيعمل إيه", "What 'membership link' does" },
                    { 6L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 3L, "session_delete_warning", "حذف الحصة نهائي", "Deleting a session is permanent" },
                    { 7L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 4L, "auto_vs_manual_code", "كود الطالب أوتوماتيك ولا يدوي", "Auto vs manual student codes" },
                    { 8L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 4L, "bulk_import", "الاستيراد الجماعي للطلاب", "Bulk importing students" },
                    { 9L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 5L, "hold_means_unrecorded", "'التعليق' بيعمل إيه", "What 'Hold' does" },
                    { 10L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 5L, "scan_flow", "امسح ← طابور ← ابعت", "Scan → queue → submit" },
                    { 11L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 6L, "withdraw_vs_refund", "السحب مقابل الاسترداد", "Withdraw vs Refund" },
                    { 12L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 6L, "proration", "ليه الطالب الجديد عليه مبلغ جزئي", "Why a new student owes a partial amount" },
                    { 13L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 6L, "departure_settlement", "محاسبة طالب بيمشي", "Settling a student who leaves" },
                    { 14L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 7L, "draft_published_closed", "دورة حياة الامتحان", "Exam lifecycle" },
                    { 15L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 7L, "oex_scope", "مين بياخد الامتحان", "Who gets the exam" },
                    { 16L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 8L, "during_vs_separate", "داخل الحصة مقابل وقت منفصل", "During-session vs Separate-time" },
                    { 17L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 9L, "unit_vs_scope", "الوحدة مقابل النطاق", "Unit vs Scope" },
                    { 18L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 9L, "video_publish", "مسودة، نشر وجدولة", "Draft, publish and schedule" },
                    { 19L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 10L, "report_types", "التقارير مقابل التصدير", "Reports vs Export" },
                    { 20L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 11L, "qr_pdf_vs_excel", "QR كـ PDF مقابل بيانات Excel", "QR PDF vs data Excel" },
                    { 21L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 12L, "what_audit_tracks", "سجل النشاط بيوري إيه", "What the audit trail shows" },
                    { 22L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 13L, "students_only", "الطلاب بس — مش الحصص", "Students only — not sessions" },
                    { 23L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 14L, "deactivate_vs_suspend", "إلغاء التفعيل مقابل الإيقاف مقابل الحذف", "Deactivate vs Suspend vs Delete" },
                    { 24L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 15L, "soft_vs_physical_qr", "QR داخل التطبيق مقابل مطبوع", "Soft QR vs Physical QR" },
                    { 25L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 15L, "proration_tiers", "شرائح البروراتا ١٠/١٠/١٠", "The 10/10/10 proration tiers" },
                    { 26L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 16L, "why_cant_i_see", "ليه مش شايف بيانات مدرسي؟", "Why can't I see my teacher's data?" },
                    { 27L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 16L, "the_two_codes", "شرح الكودين", "The two codes explained" },
                    { 28L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 17L, "student_link_lifecycle", "كل حالة مدرس معناها إيه", "What each teacher status means" },
                    { 29L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 18L, "student_attendance_view", "إزاي تقرا حضورك", "Reading your attendance" },
                    { 30L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 19L, "tracking_not_paying", "دي متابعة، مش دفع", "This is tracking, not paying" },
                    { 31L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 19L, "signed_amounts", "يعني إيه +جنيه و−جنيه", "What +LE and −LE mean" },
                    { 32L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 20L, "watch_status", "'شاهدت' مقابل 'جاري'", "Watched vs In progress" },
                    { 33L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 20L, "submit_vs_retry", "الكويز: تسليم مقابل إعادة", "Quiz: Submit vs Retry" },
                    { 34L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 21L, "exam_window_anticheat", "مواعيد الامتحان ومكافحة الغش", "Exam windows and anti-cheat" },
                    { 35L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 22L, "offline_results", "إزاي تقرا النتايج الورقية", "Reading offline results" },
                    { 36L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 23L, "assistant_no_withdraw", "ليه مفيش زر سحب", "Why there's no Withdraw button" },
                    { 37L, new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 23L, "assistant_permissions", "عناصر قايمة مش عارف تستخدمها", "Menu items you can't use" }
                });

            migrationBuilder.InsertData(
                table: "HelpTourSteps",
                columns: new[] { "Id", "AnchorKey", "BodyAr", "BodyEn", "CreateAt", "DisplayOrder", "HelpModuleId", "TitleAr", "TitleEn" },
                values: new object[,]
                {
                    { 9L, "ses_membership_link", "ربط الحصص الأسبوعية بيخلي الطالب يقدر يحضر أي واحدة منهم — مفيد لحصص التعويض أو نفس الكلاس في مواعيد مختلفة.", "Linking weekly sessions lets a student attend any of them interchangeably — handy for make-up classes or the same class at different times.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 3L, "ربط الحصص", "Membership link" },
                    { 10L, "stu_add", "ضيف طالب واحد في المرة، أو استخدم الاستيراد الجماعي عشان ترفع ملف فيه ناس كتير مرة واحدة.", "Add one student at a time, or use Bulk import to upload a spreadsheet of many at once.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 4L, "ضيف طالب", "Add a student" },
                    { 11L, "stu_code", "كل طالب ليه كود (لحد ١٠ حروف/أرقام) بيتستخدم في المسح. يا إما بيتولّد أوتوماتيك أو انت اللي بتحطه — بتتحكم فيه من الإعدادات.", "Each student has a code (up to 10 letters/numbers) used for scanning. It's either auto-generated or set by you — controlled in Settings.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 4L, "كود الطالب", "Student code" },
                    { 12L, "stu_barcode", "كل طالب ليه كارت باركود/QR التطبيق بيمسحه عشان يسجّل الحضور ويحصّل الفلوس.", "Every student has a barcode/QR card the app scans to mark attendance and collect payments.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 4L, "كارت الباركود", "Barcode card" },
                    { 13L, "att_scan", "امسح (أو اكتب) كود الطالب عشان يتحط في الطابور. المسح لوحده مبيحفظش — كمّل مسح، وبعدين ابعت الدفعة كلها.", "Scan (or type) a student code to queue them. Scanning does not save on its own — keep scanning, then submit the whole batch.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 5L, "امسح عشان تسجّل", "Scan to mark" },
                    { 14L, "att_hold", "التعليق معناه 'سيبه لبعدين'. الطلاب المعلّقين مش بيتسجّلوا لما تبعت — وده أكتر حاجة بتلخبط.", "Hold means 'skip / decide later'. Students left on Hold are NOT recorded when you submit — this is the most common surprise.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 5L, "'تعليق' مش غياب", "'Hold' is not absent" },
                    { 15L, "att_submit", "الطابور مبيتسجّلش غير لما تبعت. استخدم 'تراجع' عشان تلغي آخر تسجيل لو غلطت.", "The queue is only recorded when you submit. Use 'Revert' to undo the last mark(s) if you make a mistake.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 5L, "ابعت عشان تحفظ", "Submit to save" },
                    { 16L, "pay_collect", "سجّل فلوس من طالب. في الحصص الشهرية الدفعة بتملا أقدم شهر مش مدفوع الأول وتكمّل قدام.", "Record cash from a student. For monthly sessions the payment fills the oldest unpaid month first and cascades forward.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 6L, "تحصيل الدفع", "Collect payment" },
                    { 17L, "pay_wallet", "الفلوس اللي المساعد حصّلها بتفضل في محفظته لحد ما تسحبها. السحب بيصفّر محفظته ويسجّل التسليم.", "Cash an assistant collected sits in their wallet until you withdraw it. Withdraw resets their wallet to zero and logs the hand-over.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 6L, "محافظ المساعدين", "Assistant wallets" },
                    { 18L, "pay_departed", "لما طالب يمشي، ده بيحاسب — بيوريك مبلغ استرداد أو مبلغ مستحق — وبيلغي ربطه بالحصة (وممكن تحذفه كمان).", "When a student leaves, this settles up — showing a refund due or an amount owed — and unassigns them (optionally deleting them too).", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 6L, "طالب بيمشي", "Student leaving" },
                    { 19L, "oex_create", "اعمل امتحان اختيار من متعدد رقمي. كل سؤال ليه درجة. اسنده لحصة أو مجموعة.", "Create a digital multiple-choice exam. Each question carries a degree (its score). Assign it to a session or a group.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 7L, "اعمل امتحان", "Build an exam" },
                    { 20L, "oex_publish", "الامتحان بيبدأ مسودة. انشره عشان يوصل للطلاب. الامتحان المقفول بيظهر 'محلول'.", "An exam starts as Draft. Publish it to deliver it to students. A closed exam shows as 'solved'.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 7L, "مسودة ← منشور", "Draft → Published" },
                    { 21L, "oex_anticheat", "اختيارياً امنع الطالب لو خرج من شاشة الامتحان، مع عدد مرات خروج مسموح. النتايج بتوري كام طالب اتمنع.", "Optionally block a student if they leave the exam screen, with an allowed-leaves count. Results show how many students were blocked.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 7L, "مكافحة الغش", "Anti-cheat" },
                    { 22L, "ofex_create", "تابع الامتحانات الحضورية: حدّدها، سجّل الحضور، وادخل الدرجات. اختار تسليم داخل الحصة أو في وقت منفصل.", "Track in-person exams: schedule them, take attendance, and enter grades. Choose during-session or separate-time delivery.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 8L, "حدّد امتحان ورقي", "Schedule a paper exam" },
                    { 23L, "ofex_grades", "اكتب كل درجة من الدرجة الكاملة. فلتر بـ متصحّح / مش متصحّح. مسح الدرجة بيسيب الطالب متسجّل حاضر.", "Type each grade out of the max. Filter by Graded / Ungraded. Clearing a grade keeps the student marked as attended.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 8L, "ادخال الدرجات", "Enter grades" },
                    { 24L, "vid_unit", "الوحدة (التصنيف) بتجمع فيديوهات دروس مترابطة. كل فيديو لازم يكون في وحدة واحدة على الأقل.", "A unit (category) groups related lesson videos. Every video belongs to at least one unit.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 9L, "الوحدات", "Units" },
                    { 25L, "vid_scope", "نطاق الاستهداف هو الجمهور — أنهي حصص أو مجموعات تقدر تشوف الفيديو. الوحدة والنطاق حاجتين مختلفتين.", "The target scope is the audience — which sessions or groups can see the video. Unit and scope are two different things.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 9L, "نطاق الاستهداف", "Target scope" },
                    { 26L, "vid_analytics", "شوف مين شاف: 'شاهد' = فتح، 'أكمل' = خلّص. حذف الفيديو بيمسح تحليلاته كمان.", "See who watched: Seen = opened, Completed = watched through. Deleting a video also removes its analytics.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 9L, "تحليلات المشاهدة", "View analytics" },
                    { 27L, "rep_catalog", "اتصفّح أنواع التقارير مقسّمة على الطلاب والحضور والمدفوعات — زي 'الطلاب غير الدافعين' أو 'غياب الحصة'.", "Browse report types grouped by Students, Attendance and Payments — like 'Unpaid Students' or 'Session Absence'.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 10L, "قائمة التقارير", "Report catalog" },
                    { 28L, "exp_format", "صدّر الطلاب أو أكواد QR أو الحصص كملف PDF أو Excel. الملف بيتعمل على جهازك.", "Export students, QR codes or sessions as a PDF or Excel file. The file is generated on your device.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 11L, "اختار الصيغة", "Pick a format" },
                    { 29L, "exp_share", "لما يجهز، الملف بيفتح في قايمة المشاركة بتاعت موبايلك — مش بينزل في فولدر التنزيلات.", "When it's ready, the file opens in your phone's share sheet — it isn't dropped into a downloads folder.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 11L, "شيره", "Share it" },
                    { 30L, "audit_filter", "شوف المساعدين عملوا إيه — إضافة / تعديل / إلغاء تفعيل / حذف / عرض — وفلتر بنوع الإجراء والقسم والتاريخ.", "See what your assistants did — Add / Edit / Deactivate / Delete / View — and filter by action type, module and date range.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 12L, "راجع نشاط المساعدين", "Review assistant activity" },
                    { 31L, "recycle_restore", "الطلاب المحذوفين بيفضلوا هنا ١٠ أيام، وبعدين بيتمسحوا. الاسترجاع بيرجّع الطالب — بس من غير حصة، فاربطه تاني.", "Deleted students stay here for 10 days, then are purged. Restore brings a student back — but WITHOUT a session, so re-assign them.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 13L, "استرجاع الطلاب", "Restore students" },
                    { 32L, "asst_create", "اعمل حساب مساعد واختار بالظبط أنهي صلاحيات ياخدها — لازم واحدة على الأقل.", "Create an assistant account and choose exactly which permissions they get — at least one is required.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 14L, "اعمل مساعد", "Create an assistant" },
                    { 33L, "asst_permissions", "بعض الصلاحيات متعلّم عليها 'مقيّدة' (زي تعديل الحضور القديم). المساعد اللي مالوش الصلاحية بيتمنع من الإجراء ده.", "Some permissions are marked 'Restricted' (e.g. editing past attendance). An assistant without one is blocked from that action.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 14L, "الصلاحيات", "Permissions" },
                    { 34L, "set_identification", "اختار إذا كانت أكواد الطلاب بتتولّد أوتوماتيك ولا انت بتحطها. ده بيحدد الكود اللي بيظهر في إضافة الطالب.", "Choose whether student codes are generated automatically or set by you. This drives the code shown on Add Student.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 15L, "الأكواد: أوتوماتيك ولا يدوي", "Codes: auto or manual" },
                    { 35L, "set_qr_mode", "الـ QR داخل التطبيق بيوري كود كل طالب في تطبيقه. الـ QR المطبوع بيخفي الكود من التطبيق لأنك بتوزّع كروت مطبوعة بدلها.", "Soft QR shows each student's code in their app. Physical QR hides the in-app code because you hand out printed cards instead.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 15L, "QR داخل التطبيق ولا مطبوع", "Soft vs Physical QR" },
                    { 36L, "set_proration", "شرائح أول/تاني/تالت ١٠ أيام بتحدّد الطالب اللي بيدخل في نص الشهر عليه كام. ده بيغذّي علامة 'جزئي' في المدفوعات.", "The First / Second / Third 10-day tiers decide how much a student who joins mid-month owes. This feeds the 'Prorated' payment badge.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 15L, "شرائح البروراتا", "Proration tiers" },
                    { 37L, "lk_add_teacher", "دوس ضيف مدرس، وبعدين اكتب كود المدرس بالأرقام واسمك. سيب خانة كود الطالب فاضية لو المدرس مداكش كود.", "Tap Add teacher, then enter your teacher's numeric code and your name. Leave the student code empty if the teacher didn't give you one.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 16L, "ضيف مدرسك", "Add your teacher" },
                    { 38L, "lk_status", "بعد ما تبعت الطلب حالته بتبقى 'قيد الانتظار'. لازم مدرسك يوافق — مجرد إرسال الطلب مبيربطكش.", "After you send the request its status is 'Pending'. Your teacher has to approve it — sending the request alone does not link you.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 16L, "استنى الموافقة", "Wait for approval" },
                    { 39L, "lk_awaiting", "'بانتظار الربط' معناها إن مدرسك وافق على حسابك بس لسه مربطكش بسجلك — علشان كده لسه مش شايف حاجة. اطلب من مدرسك يربطك.", "'Awaiting link' means your teacher approved your account but hasn't linked you to your record yet — so you still see nothing. Ask your teacher to link you.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 3, 16L, "الوصل لوحده مش كفاية", "Connected isn't enough" },
                    { 40L, "lk_locked", "أي قسم رمادي أو مكتوب عليه 'مخفي بواسطة المدرس' معناه إن المدرس قافل القسم ده للطلاب. دي مش مشكلة في التطبيق.", "A greyed or 'hidden by teacher' tile means the teacher turned that section off for students. It is not a bug.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 4, 16L, "الأقسام المخفية", "Hidden modules" },
                    { 41L, "shome_add", "استخدم زر الإضافة عشان تبعت طلب ربط لمدرس بالكود بتاعه. مدرسينك بيظهروا هنا بحالتهم.", "Use the add button to send a link request to a teacher with their code. Your teachers appear here with their status.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 17L, "ضيف مدرس", "Add a teacher" },
                    { 42L, "shome_card", "بس المدرسين 'النشطين' اللي تقدر تدوس عليهم. الحالات التانية (قيد الانتظار، بانتظار الربط) لسه مستنية — الدوس بيوري تنبيه، مش بياناتهم.", "Only 'Active' teachers are tappable. Other statuses (Pending, Awaiting link) are still waiting — tapping shows a hint, not their data.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 17L, "افتح مدرس", "Open a teacher" },
                    { 43L, "satt_ring", "ده بيوري نسبة حضورك وتاريخ حضورك وغيابك مع المدرس. للعرض بس.", "This shows your attendance percentage and your present/absent history for the teacher. It's read-only.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 18L, "حضورك", "Your attendance" },
                    { 44L, "spay_status", "شوف انت دفعت إيه وعليك إيه. مفيش 'ادفع دلوقتي' هنا — دي شاشة متابعة؛ انت بتدفع لمدرسك مباشرة.", "See what you've paid and what's due. There is no 'Pay now' here — it's a tracking screen; you pay your teacher directly.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 19L, "حالة دفعك", "Your payment status" },
                    { 45L, "spay_overdue", "دوس عشان توسّع المدفوع والمتأخر. المدفوع/القادم بيظهر +جنيه؛ المتأخر بيظهر −جنيه — والناقص ده اللي لسه عليك، مش رسوم.", "Tap to expand Paid and Overdue. Paid/upcoming amounts show as +LE; overdue shows as −LE — that minus is what you still owe, not a charge.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 19L, "المدفوع والمتأخر", "Paid and Overdue" },
                    { 46L, "svid_status", "كل درس بيوري 'شاهدت'، 'جاري'، أو 'مبدأتش' — بتتحسب أوتوماتيك من مشاهدتك الفعلية، مش يدوي.", "Each lesson shows Watched, In progress or Not started — tracked automatically from your real playback, not set manually.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 20L, "حالة المشاهدة", "Watch status" },
                    { 47L, "svid_quiz", "لو الدرس فيه كويز، بيظهر زر 'ابدأ الكويز'. في آخر سؤال الزر بيقول تسليم — أو إعادة لو لازم تعيد قبل ما تسلّم تاني.", "If a lesson has a quiz, a 'Start quiz' button appears. On the last question the button says Submit — or Retry if you must retake before submitting again.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 20L, "كويز الدرس", "Lesson quiz" },
                    { 48L, "soex_instructions", "قبل الامتحان هتشوف عدد الأسئلة والدرجة الكلية والقواعد. للامتحانات المراقَبة فيه تحذير مكافحة غش مع عدد أقصى للمخالفات.", "Before an exam you'll see the question count, total degree and rules. For proctored exams there's an anti-cheat warning with a max-violations count.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 21L, "اقرا التعليمات", "Read the instructions" },
                    { 49L, "soex_start", "دوسة ابدأ بتبدأ المحاولة المؤقتة — ومبترجعش. الخروج من امتحان مراقَب بيتحسب مخالفة؛ كتر المخالفات بيمنعك.", "Tapping Start begins the timed attempt — it can't be undone. Leaving a proctored exam counts as a violation; too many blocks you.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 21L, "'ابدأ' بيبدأ محاولتك", "'Start' begins your attempt" },
                    { 50L, "sofex_result", "دي بتوري نتايج امتحاناتك الورقية/الحضورية — للعرض بس. الشارة الخضرا هي درجتك؛ الشارة الحمرا 'غائب' معناها إنك مدخلتش الامتحان.", "This lists your paper/in-class exam results — read-only. A green chip is your score; a red 'Missed' chip means you didn't sit it.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 22L, "نتايج امتحاناتك الحضورية", "Your in-person results" },
                    { 51L, "asst_cash_bag", "'معاك دلوقتي' هو الكاش اللي في إيدك حالياً. 'إجمالي التحصيل' هو إجماليك كله. رقمين مختلفين.", "'Holding now' is the cash in your hand right now. 'Total collected' is your lifetime total. They're different numbers.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, 23L, "الكاش اللي معاك", "Cash you're holding" },
                    { 52L, "asst_collect", "سجّل فلوس من طالب هنا. بتتضاف لمحفظتك لحد ما المدرس يسحبها منك.", "Record cash from a student here. It adds to your wallet until the teacher withdraws it from you.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, 23L, "تحصيل الدفع", "Collect payment" }
                });

            migrationBuilder.InsertData(
                table: "HelpArticleSections",
                columns: new[] { "Id", "BodyAr", "BodyEn", "CreateAt", "DisplayOrder", "HeadingAr", "HeadingEn", "HelpArticleId" },
                values: new object[,]
                {
                    { 8L, "الحصص الأسبوعية المربوطة بتتشارك في الحضور: الطالب اللي اتسجّل حاضر في أي حصة مربوطة بيتحسب حاضر للكلاس. بس الحصص الأسبوعية اللي تقدر تتربط بأسبوعية.", "Linked weekly sessions share attendance: a student marked present in any linked session counts for the class instance. Only weekly sessions can link to weekly sessions.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 5L },
                    { 9L, "على عكس الطلاب، الحصة المحذوفة مبترجعش من سلة المحذوفات. انقل الطلاب برّه الأول لو محتاج تحتفظ بيهم.", "Unlike students, a deleted session cannot be restored from the recycle bin. Transfer students out first if you need to keep them.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 6L },
                    { 10L, "من الإعدادات بتختار إذا كان التطبيق هو اللي يولّد كود كل طالب أوتوماتيك، ولا انت اللي تكتب كل كود بإيدك. الكود ميتعادش استخدامه وهو نشط، فالتكرار بيترفض.", "In Settings you choose whether the app assigns each student code automatically, or you type every code yourself. A code can't be reused while it's active, so duplicates are rejected.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 7L },
                    { 11L, "نزّل القالب، املاه، وارفع ملف .csv/.xlsx. شاشة النتيجة بتفصل الطلاب اللي اتضافوا عن اللي فشلوا (بالصف).", "Download the template, fill it, and upload the .csv/.xlsx. The result screen splits Imported students from Import failures (by row).", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, "الخطوات", "The steps", 8L },
                    { 12L, "لو اسم حصة في الملف مش متطابق مع حصصك، الطالب بيتضاف برضه — بس من غير حصة. اربطه بحصة بعد كده.", "If a session name in the sheet doesn't match one of yours, the student is still imported — just without a session. Assign them afterward.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "أسماء الحصص غير المتطابقة", "Unmatched sessions", 8L },
                    { 13L, "التعليق بيأجّل الطالب من غير قرار. لما تبعت، المعلّقين بيتساب — لا حاضر ولا غايب. ارجعله وسجّله صح.", "Hold parks a student without deciding. On submit, Held students are skipped — not marked absent, not marked present. Come back and mark them properly.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 9L },
                    { 14L, "المسح بيضيف كل طالب للطابور. مفيش حاجة بتتحفظ غير لما تبعت الدفعة. لو مسحت نفس الطالب مرتين بيقولك 'اتسجّل النهاردة'.", "Scanning adds each student to a queue. Nothing is saved until you submit the batch. Scanning the same student twice shows 'already recorded today'.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 10L },
                    { 15L, "تعديل حضور يوم فات لصاحب الحساب بس. المساعدين محتاجين صلاحية 'تعديل الحضور القديم' من المدرس.", "Editing attendance for a past day is owner-only. Assistants need the 'Edit past attendance' permission granted by the tutor.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "تعديل أيام فاتت", "Editing past days", 10L },
                    { 16L, "تاخد الفلوس من محفظة المساعد لإيدك. بيصفّر رصيد محفظة المساعد وبيتسجّل في سجل السحوبات.", "Taking cash from an assistant's wallet into your hands. It resets that assistant's wallet balance to zero and is recorded in the withdrawal history.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, "السحب", "Withdraw", 11L },
                    { 17L, "ترجّع فلوس لطالب. بيتسجّل كقيمة بالسالب على المُحصّل الأصلي — إجراء مختلف تماماً عن السحب.", "Giving money back to a student. It's recorded as a negative entry against the original collector — a completely different action from a withdrawal.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "الاسترداد", "Refund", 11L },
                    { 18L, "الطالب اللي بيدخل في نص الشهر بيتحاسب على شهر أول جزئي (بروراتا)، حسب شرائح أول/تاني/تالت ١٠ أيام اللي بتحطها في الإعدادات. شاشة النتيجة بتوري علامة 'جزئي'.", "A student who joins mid-month is charged a prorated (partial) first month, based on the First/Second/Third 10-day tiers you set in Settings. The result screen shows a 'Prorated' badge.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 12L },
                    { 19L, "المغادرة بتحسب استرداد (انت مدينله)، أو مبلغ مستحق (هو مدينلك)، أو مفيش حاجة — من حضوره واللي دفعه. التأكيد بيلغي ربط الطالب بالحصة.", "Departure computes a refund (you owe them), an amount owed (they owe you), or nothing to settle — from their attendance and what they paid. Confirming it unassigns the student from the session.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 13L },
                    { 20L, "مسودة (قابل للتعديل، مش ظاهر) ← منشور (شغّال للطلاب) ← مقفول (بيظهر 'محلول'). تعديل امتحان منشور بيحتاج النسخة الحالية، فالتعديلات القديمة بتترفض.", "Draft (editable, not visible) → Published (live for students) → Closed (shows as 'solved'). Editing a published exam requires the current version, so stale edits are rejected.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 14L },
                    { 21L, "الامتحانات بتتسند لحصة أو مجموعة — مش لطلاب فرادى. استخدم 'إظهار النتايج' للتحكم إذا كان الطلاب يشوفوا نتيجتهم ولا لأ.", "Exams target a session or a session group — not individual students. Use 'Show results' to control whether students can see their result.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 15L },
                    { 22L, "الامتحان بيحصل جوه كلاس عادي، فحضوره للقراءة بس — بيتسحب من حضور الحصة.", "The exam happens inside a normal class, so its attendance is read-only — pulled from the class session's attendance.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, "داخل الحصة", "During session", 16L },
                    { 23L, "الامتحان ليه تاريخه، وحضوره، ومسح QR بتاعه، مستقل عن أي كلاس.", "The exam has its own date, its own attendance, and its own QR scan, independent of any class.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "وقت منفصل", "Separate time", 16L },
                    { 24L, "الوحدة هي طريقة ترتيب الفيديوهات (زي الفولدر). نطاق الاستهداف هو مين يقدر يشوفهم (الجمهور). الفيديو ممكن يكون في وحدة ومش ظاهر لحد لحد ما تحدّد نطاقه.", "A unit is how videos are ORGANISED (a folder). The target scope is WHO can see them (the audience). A video can be in a unit yet visible to no one until you set its scope.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 17L },
                    { 25L, "الفيديو بيفضل مسودة لحد ما تنشره. تقدر كمان تحدّد تاريخ نشر عشان يطلع بعدين. الطلاب بيشوفوا الفيديوهات المنشورة اللي في نطاقهم بس.", "A video is Draft until you publish it. You can also set a publish date to release it later. Students only ever see published videos in their scope.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 18L },
                    { 26L, "التقارير بتخليك تختار نوع تقرير وتشوف الفلاتر قبل ما تولّده. لتصدير قوايم خام (طلاب، كروت QR، حصص) استخدم شاشة التصدير بدل كده.", "Reports let you pick a report type and see its filters before generating. For exporting raw lists (students, QR cards, sessions) use the Export flow instead.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 19L },
                    { 27L, "من الطلاب تقدر تصدّر حاجتين مختلفتين: 'أكواد QR (PDF)' كروت قابلة للطباعة والمسح؛ 'قائمة الطلاب (Excel)' جدول بيانات. اختار حسب اللي محتاجه.", "From students you can export two different things: 'QR Codes (PDF)' is printable scannable cards; 'Students List (Excel)' is a data table. Pick by what you need.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 20L },
                    { 28L, "بيتابع إجراءات مساعدينك، مش الطلاب. كل سطر بيقول '{الإجراء} · {القسم}'. التصدير Excel بس.", "It tracks your assistants' actions, not students. Each entry reads '{action} · {module}'. Export is Excel-only.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 21L },
                    { 29L, "الطلاب بس اللي يترجعوا، خلال ١٠ أيام. الحصص بتتحذف نهائي ومبتظهرش في سلة المحذوفات.", "Only students can be restored, within a 10-day window. Sessions are deleted permanently and never appear in the recycle bin.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 22L },
                    { 30L, "إلغاء التفعيل بيوقف الدخول (قابل للرجوع). الإيقاف والحذف إجراءات منفصلة وأقوى. فلوس المساعد اللي حصّلها بتفضل في محفظته لحد ما تسحبها.", "Deactivate disables sign-in (reversible). Suspend and Delete are separate, stronger actions. An assistant's collected cash stays in their wallet until you withdraw it.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 23L },
                    { 31L, "داخل التطبيق = الطلاب بيوروا كودهم من جوه التطبيق. مطبوع = انت بتطبع وتوزّع كروت، والكود في التطبيق بيتخفي. اختار اللي يناسب طريقة مسحك على الباب.", "Soft QR = students show their code from inside the app. Physical QR = you print and hand out cards, and the in-app code is hidden. Pick whichever matches how you scan at the door.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 24L },
                    { 32L, "الطالب اللي بيدخل في أول ١٠ أيام، أو تاني ١٠، أو تالت ١٠ من الشهر بيتحاسب على جزء مختلف من الشهر. حدّدهم هنا؛ بيغذّوا كل مبلغ 'جزئي' في المدفوعات.", "A student joining in the first 10 days, second 10, or third 10 of the month is charged a different share of the month. Set these here; they drive every 'Prorated' amount in Payments.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 25L },
                    { 33L, "بس المدرس اللي حالته 'نشط' (مربوط) بيفتحلك المحتوى. 'قيد الانتظار' مستني الموافقة؛ و'بانتظار الربط' معناها إنك موصول بس لسه مش مربوط.", "Only an 'Active' (linked) teacher opens content. 'Pending' waits for approval; 'Awaiting link' means you're connected but not linked yet.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, "شوف حالتك", "Check your status", 26L },
                    { 34L, "حتى لو مربوط، المدرس ممكن يخفي الحضور أو المدفوعات أو الواجب أو الامتحانات. الأقسام المخفية بتظهر مقفولة أو مبتظهرش خالص.", "Even when linked, a teacher can hide Attendance, Payments, Homework or Exams. Hidden sections show a locked tile or don't appear at all.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "يمكن المدرس أخفاها", "The teacher may have hidden it", 26L },
                    { 35L, "'الكود بتاعي' بيعرّفك وانت بتربط بمدرس. أما كود الحضور (QR) فحاجة تانية — ده اللي مدرسك بيمسحه عشان يسجّلك حاضر في الحصة. متخلطش بينهم.", "'My code' identifies you when linking to a teacher. Your attendance QR is different — it's the code your teacher scans to mark you present in class. Don't confuse the two.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 27L },
                    { 36L, "نشط = انت مربوط وبتشوف كل اللي المدرس بيشاركه. قيد الانتظار = مستني الموافقة. بانتظار الربط = اتوافق بس لسه مش مربوط بسجلك. مرفوض / متشال بواسطة المدرس = الربط انتهى.", "Active = you're linked and can see everything the teacher shares. Pending = waiting for approval. Awaiting link = approved but not yet linked to your record. Declined / Removed by teacher = the link ended.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 28L },
                    { 37L, "الدايرة بتوري نسبتك؛ القايمة بتوري كل يوم كلاس وانت كنت حاضر ولا غايب. لو مدرسك أخفى الحضور، القسم ده بيبقى مقفول أو مش موجود.", "The ring shows your percentage; the list shows each class day and whether you were present or absent. If your teacher hides attendance, this section is locked or missing.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 29L },
                    { 38L, "مش بتقدر تدفع جوه التطبيق — الشاشة دي بتوري تاريخك واللي عليك بس. سلّم دفعتك لمدرسك؛ هو بيسجّلها وبتظهر هنا.", "You can't pay inside the app — this screen only shows your history and what's due. Hand your payment to your teacher; they record it and it appears here.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 30L },
                    { 39L, "المبالغ اللي دفعتها أو القادمة بتظهر +جنيه. الشهر المتأخر بيظهر −جنيه — المبلغ اللي لسه عليك. الخطط الشهرية بتوري شهور؛ خطط بالحصة بتوري تواريخ.", "Amounts you've paid or that are upcoming show as +LE. An overdue month shows as −LE — the amount you still owe. Monthly plans show months; per-session plans show dates.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 31L },
                    { 40L, "دي بتتحدّث أوتوماتيك وانت بتشغّل الدرس: مبدأتش، جاري، وبعدين شاهدت لما تخلّص. انت مش بتحطها بنفسك.", "These update automatically as you play a lesson: Not started, In progress, then Watched once you finish. You don't set them yourself.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 32L },
                    { 41L, "في آخر سؤال الزر بيبقى تسليم — إلا لو محاولتك خلصت، ساعتها بيبقى إعادة (اعيد الأول). بعد التسليم، 'إعادة' بتظهر بس لو مدرسك سمح بالإعادة.", "On the last question the button is Submit — unless your attempt is used up, when it becomes Retry (retake first). After submitting, 'Retry' only appears if your teacher allowed retakes.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 33L },
                    { 42L, "لو فتحت امتحان قبل ميعاد بدايته هتشوف 'لسه مبدأش — بيبدأ الساعة …' وعد تنازلي. مش هتقدر تدخل بدري.", "If you open an exam before its start time you'll see 'Not started — starts at …' and a countdown. You can't enter early.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, "قبل الميعاد", "Before the window", 34L },
                    { 43L, "في الامتحان المراقَب، الخروج من الشاشة بيتحسب مخالفة. تعدّي الحد بيوري 'ممنوع' ويفتح نتيجة للعرض بس. لما العد يخلص الامتحان بيتسلّم أوتوماتيك.", "In a proctored exam, leaving the screen counts as a violation. Exceeding the limit shows 'Blocked' and opens a read-only result. When the timer hits zero the exam auto-submits.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 2, "المراقبة", "Proctoring", 34L },
                    { 44L, "كل كارت بيوري المادة، ودرجتك من الدرجة الكاملة، والتاريخ. 'غائب' معناها إن الامتحان اتعمل بس انت متسجّلتش حاضر.", "Each card shows the subject, your score out of the max, and the date. 'Missed' means the exam was held but you weren't marked as attending.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 35L },
                    { 45L, "المدرس بس اللي يقدر يسحب الكاش من محفظتك. انت بتمسك الكاش وتسلّمه؛ المدرس بيسجّل السحب، واللي بيصفّر محفظتك.", "Only the teacher can withdraw cash from your wallet. You hold the cash and hand it over; the teacher records the withdrawal, which resets your wallet to zero.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 36L },
                    { 46L, "ممكن تشوف عناصر مالكش صلاحية فيها — المدرس بيتحكم في صلاحياتك، فبعض الإجراءات بتوري خطأ. اطلب من مدرسك يديك اللي محتاجه.", "You may see menu items you don't have permission for — the teacher controls your permissions, so some actions show an error. Ask your teacher to grant what you need.", new DateTime(2026, 8, 14, 0, 0, 0, 0, DateTimeKind.Utc), 1, null, null, 37L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 19L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 28L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 32L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 33L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 34L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 35L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 36L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 37L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 38L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 39L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 40L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 41L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 42L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 43L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 44L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 45L);

            migrationBuilder.DeleteData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 46L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 19L);

            migrationBuilder.DeleteData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 19L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 28L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 32L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 33L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 34L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 35L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 36L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 37L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 38L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 39L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 40L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 41L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 42L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 43L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 44L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 45L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 46L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 47L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 48L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 49L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 50L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 51L);

            migrationBuilder.DeleteData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 52L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 19L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 28L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 32L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 33L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 34L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 35L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 36L);

            migrationBuilder.DeleteData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 37L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 19L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 20L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 21L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 23L);

            migrationBuilder.UpdateData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "BodyAr", "BodyEn", "HeadingAr", "HeadingEn" },
                values: new object[] { "بس المدرس اللي حالته 'نشط' (مربوط) بيفتحلك المحتوى. 'قيد الانتظار' مستني الموافقة؛ و'بانتظار الربط' معناها إنك موصول بس لسه مش مربوط.", "Only an 'Active' (linked) teacher opens content. 'Pending' waits for approval; 'Awaiting link' means you're connected but not linked yet.", "شوف حالتك", "Check your status" });

            migrationBuilder.UpdateData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "BodyAr", "BodyEn", "HeadingAr", "HeadingEn" },
                values: new object[] { "حتى لو مربوط، المدرس ممكن يخفي الحضور أو المدفوعات أو الواجب أو الامتحانات. الأقسام المخفية بتظهر مقفولة أو مبتظهرش خالص.", "Even when linked, a teacher can hide Attendance, Payments, Homework or Exams. Hidden sections show a locked tile or don't appear at all.", "يمكن المدرس أخفاها", "The teacher may have hidden it" });

            migrationBuilder.UpdateData(
                table: "HelpArticleSections",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "BodyAr", "BodyEn" },
                values: new object[] { "'الكود بتاعي' بيعرّفك وانت بتربط بمدرس. أما كود الحضور (QR) فحاجة تانية — ده اللي مدرسك بيمسحه عشان يسجّلك حاضر في الحصة. متخلطش بينهم.", "'My code' identifies you when linking to a teacher. Your attendance QR is different — it's the code your teacher scans to mark you present in class. Don't confuse the two." });

            migrationBuilder.UpdateData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "Key", "TitleAr", "TitleEn" },
                values: new object[] { "why_cant_i_see", "ليه مش شايف بيانات مدرسي؟", "Why can't I see my teacher's data?" });

            migrationBuilder.UpdateData(
                table: "HelpArticles",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "DisplayOrder", "HelpModuleId", "Key", "TitleAr", "TitleEn" },
                values: new object[] { 2, 2L, "the_two_codes", "شرح الكودين", "The two codes explained" });

            migrationBuilder.UpdateData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 3L,
                columns: new[] { "AnswerAr", "AnswerEn", "DisplayOrder", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[] { "طلبك 'قيد الانتظار' لحد ما مدرسك يوافق. مفيش حاجة تانية عليك — استنى الموافقة.", "Your request is 'Pending' until your teacher approves it. There's nothing else to do on your side — wait for the approval.", 1, "linking", 2, "بعت طلب بس مفيش حاجة حصلت.", "I sent a request but nothing happened." });

            migrationBuilder.UpdateData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 4L,
                columns: new[] { "AnswerAr", "AnswerEn", "DisplayOrder", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[] { "انت في حالة 'بانتظار الربط': موصول بس لسه مش مربوط بسجلك. اطلب من مدرسك يربطك بسجلك.", "You're in 'Awaiting link': connected but not yet linked to your student record. Ask your teacher to link you to your record.", 2, "linking", 2, "مدرسي وافق عليّا بس لسه مش شايف حاجة.", "My teacher approved me but I still see nothing." });

            migrationBuilder.UpdateData(
                table: "HelpFaqItems",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "AnswerAr", "AnswerEn", "DisplayOrder", "ModuleKey", "Persona", "QuestionAr", "QuestionEn" },
                values: new object[] { "مدرسك يقدر يخفي الحضور أو المدفوعات أو الواجب أو الامتحانات. القسم المقفول (أو اللي مش ظاهر) معناه إنه متقفل للطلاب — مش عطل.", "Your teacher can hide Attendance, Payments, Homework or Exams. A locked tile (or a section that doesn't appear) means it's turned off for students — not a bug.", 3, "linking", 2, "ليه فيه قسم مقفول أو مش موجود؟", "Why is a section locked or missing?" });

            migrationBuilder.UpdateData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 1L,
                column: "DisplayOrder",
                value: 1);

            migrationBuilder.UpdateData(
                table: "HelpModules",
                keyColumn: "Id",
                keyValue: 2L,
                columns: new[] { "Key", "Persona", "TitleAr", "TitleEn" },
                values: new object[] { "linking", 2, "الربط بمدرس", "Linking to a teacher" });

            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 5L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "TitleAr", "TitleEn" },
                values: new object[] { "lk_add_teacher", "دوس ضيف مدرس، وبعدين اكتب كود المدرس بالأرقام واسمك. سيب خانة كود الطالب فاضية لو المدرس مداكش كود.", "Tap Add teacher, then enter your teacher's numeric code and your name. Leave the student code empty if the teacher didn't give you one.", "ضيف مدرسك", "Add your teacher" });

            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 6L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "TitleAr", "TitleEn" },
                values: new object[] { "lk_status", "بعد ما تبعت الطلب حالته بتبقى 'قيد الانتظار'. لازم مدرسك يوافق — مجرد إرسال الطلب مبيربطكش.", "After you send the request its status is 'Pending'. Your teacher has to approve it — sending the request alone does not link you.", "استنى الموافقة", "Wait for approval" });

            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 7L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "DisplayOrder", "HelpModuleId", "TitleAr", "TitleEn" },
                values: new object[] { "lk_awaiting", "'بانتظار الربط' معناها إن مدرسك وافق على حسابك بس لسه مربطكش بسجلك — علشان كده لسه مش شايف حاجة. اطلب من مدرسك يربطك.", "'Awaiting link' means your teacher approved your account but hasn't linked you to your record yet — so you still see nothing. Ask your teacher to link you.", 3, 2L, "الوصل لوحده مش كفاية", "Connected isn't enough" });

            migrationBuilder.UpdateData(
                table: "HelpTourSteps",
                keyColumn: "Id",
                keyValue: 8L,
                columns: new[] { "AnchorKey", "BodyAr", "BodyEn", "DisplayOrder", "HelpModuleId", "TitleAr", "TitleEn" },
                values: new object[] { "lk_locked", "أي قسم رمادي أو مكتوب عليه 'مخفي بواسطة المدرس' معناه إن المدرس قافل القسم ده للطلاب. دي مش مشكلة في التطبيق.", "A greyed or 'hidden by teacher' tile means the teacher turned that section off for students. It is not a bug.", 4, 2L, "الأقسام المخفية", "Hidden modules" });
        }
    }
}
