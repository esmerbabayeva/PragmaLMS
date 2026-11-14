# Pragma LMS — Master Kontekst və Mühəndislik Qaydaları (v12)
**Final Layihə #20 · Təhvil: 30 Noyabr 2026 · .NET 10 · Mərhələli və təsdiqli icra**

---

## 1. Rol və Dəmir Qayda (Agentə Təlimat)
Sən Pragma LMS layihəsində işləyən senior .NET mühəndisisən. Layihə Code Academy final layihəsidir və müəllimlər kodu, GitHub tarixçəsini, sistem dizaynını və canlı demonu qiymətləndirəcək. Aşağıdakı qaydalar pozulmaz:

1. **Əvvəl oxu, sonra yaz:** Hər mərhələyə başlamazdan əvvəl solution-u, mövcud entity-ləri, servisləri, controller-ləri və testləri oxu. Artıq mövcud olanı yenidən yazma, genişləndir. Mövcud kodla planın fərqini mərhələ hesabatında yaz.
2. **Commit-dən əvvəl təsdiq:** Heç bir kod istifadəçinin yoxlayıb «İşləyir, təsdiq edirəm» deməsindən əvvəl commit olunmur. `git push` etmə, push-u istifadəçi edir.
3. **Hər mərhələ üçün dövr:**
   - (a) Qısa plan yaz
   - (b) Kodu yaz
   - (c) `dotnet build` 0 xəta və 0 yeni xəbərdarlıqla
   - (d) Bütün testlər yaşıl
   - (e) Dayan və hesabat ver
4. **Hesabat formatı (Hər mərhələ sonunda):**
   - Nə hazırdır
   - Əlavə/dəyişən fayllar
   - Yeni endpoint-lər
   - Miqrasiya adları
   - Qəbul meyarları (✅/❌)
   - İstifadəçi üçün addım-addım yoxlama təlimatı
   - Planla fərqlər və açıq suallar
   - Təklif olunan commit mesajları
5. **Sirlər koda yazılmır:** Google ClientId, Gemini açarı, SMTP parolu, JWT açarı yalnız `dotnet user-secrets` və ya mühit dəyişənlərindədir. `appsettings.json`-da boş və ya nümunə dəyər qalır. Commit-dən əvvəl gitleaks və ya əl ilə axtarış et.
6. **Kod stili:** Minimum şərh (kodun özü danışmalıdır), ingilis identifikatorlar, kiçik metodlar, bir sinif bir məsuliyyət, async hər yerdə `CancellationToken` ilə.
7. **Əhatədən çıxma:** Mərhələdə yazılmayan funksiya əlavə etmə. Maraqlı ideya olsa, hesabatın sonunda təklif et.

---

## 2. Texniki Sabit Qərarlar
| Mövzu | Qərar |
| :--- | :--- |
| **Platforma** | .NET 10, ASP.NET Core, EF Core 10, SQL Server |
| **Arxitektura** | Onion: Domain, Application, Infrastructure, Api, Admin (MVC), UnitTests, IntegrationTests. Asılılıqlar yalnız daxilə |
| **Portal (Tələbə & Müəllim)** | API + JS: ASP.NET Core Web API, frontend HTML5 + Alpine.js 3 + Tailwind CSS, Fetch API. Razor/MVC yoxdur |
| **Admin Panel** | MVC + API: müstəqil ASP.NET Core MVC layihəsi (port 5089), API-yə typed ApiClient (HttpClient) ilə qoşulur. Admin birbaşa DB-yə toxunmur |
| **Daxili SPA Admin Konsolu** | Varsa yeni funksiya əlavə etmə, MVC admin əsasdır. Təkrarı aradan qaldırmaq üçün auditdə təklif ver |
| **Giriş** | Şifrə + Google Sign-In + e-poçt (Gmail daxil) ilə şifrəsiz kod/link. JWT access + refresh token rotation |
| **Biznes Xətaları** | `Result<T>` pattern (Exception yalnız gözlənilməz hallar üçün) |
| **Xəta Formatı** | Hər yerdə RFC 7807 ProblemDetails + traceId |
| **Servis Qatı** | Service–Repository. MediatR yoxdur (KISS) |
| **Entity** | BaseEntity (Id Guid, CreatedUtc, UpdatedUtc, IsDeleted, RowVersion). Soft delete üçün global query filter |
| **Keş** | HybridCache, tag əsaslı invalidasiya |
| **API Sənədi** | Scalar (OpenAPI). Hər endpoint-də summary, nümunə, auth tələbi və xəta cavabları |
| **Vaxt** | DB-də UTC, UI-da Asia/Baku. Bal hesabları decimal |
| **Dillər** | AZ (default), EN, RU. Dark/Light |
| **Versiyalama** | `/api/v1/...` |

---

## 3. Akademik Biznes Qaydaları (Dəyişməz, Serverdə Tətbiq Olunur)

### 3.1 Qiymətləndirmə (Cohort Əsaslı Kurslar)
* **Yekun bal = Quiz ortalaması × 30% + Mini layihə ortalaması × 30% + Final layihə × 40%**
* **Çəkilər kodda sabit deyil:** Cohort-da `QuizWeight=30`, `MiniProjectWeight=30`, `FinalWeight=40` (cəmi 100 olmalıdır, validasiya). Default 30/30/40.
* **Quiz komponenti hesablanır, əl ilə daxil edilmir:** Tələbənin cohort kursundakı quiz-lərin hər biri üzrə ən yaxşı cəhdi götürülür, ortalaması alınır.
* **Mini layihə və Final əl ilə daxil edilir:** Hər bal 0–100.
* **Müvəqqəti (Provisional) bal:** Hələ qiymətlənməmiş komponentin çəkisi qalan komponentlərə mütənasib bölünür və nəticə `IsProvisional=true` ilə qaytarılır. Cohort Finished olduqda və ya Final qiymətləndikdə bal rəsmi olur.
* **Yuvarlaqlaşdırma:** 1 onluq, `MidpointRounding.AwayFromZero`. Rəsmi dəyər yuvarlaqlaşdırılmış dəyərdir və hər yerdə (UI, sertifikat, Excel, müqayisə) eyni dəyər istifadə olunur.
* UI-da bu bal «Yekun bal» adlanır. «GPA» yalnız tələbənin bütün kursları üzrə ortalamadır.

### 3.2 Davamiyyət
* **Statuslar:** `Present`, `Absent`, `Excused`, `Late`. `Excused` qayıb sayılmır. `Late`: cohort-da `LateToAbsenceRatio` (default 3): hər 3 gecikmə 1 qayıb sayılır.
* `MaxAbsenceLimit` (default 6). **Qayıb sayı = Absent + floor(Late / ratio)**.
* Qayıb sayı limitin 2 vahid altına çatdıqda (default 4-cü qayıb) tələbəyə xəbərdarlıq bildirişi gedir.
* Limit aşıldıqda (> MaxAbsenceLimit) tələbə statusu `Dropped` olur. Bu avtomatikdir, lakin admin geri qaytara bilər və hər dəyişiklik `AuditLog`-a yazılır.
* Davamiyyəti yalnız həmin Cohort-un müəllimi (və Admin) yaza bilər. Müəllim 7 gün ərzində düzəliş edə bilər, sonra yalnız Admin.

### 3.3 Sertifikat Qərar Cədvəli (Cohort Əsaslı Kurs)
| Şərt | Nəticə |
| :--- | :--- |
| **Rəsmi Yekun bal ≥ 70.0 və qayıb ≤ limit** | Diplom (`CertificateType.Diploma`) |
| **Rəsmi Yekun bal < 70.0 və qayıb ≤ limit** | Dərsdə iştirak sertifikatı (`CertificateType.Attendance`) |
| **Qayıb > limit (Dropped)** | Heç bir sertifikat verilmir |
| **Cohort Finished deyil və ya bal müvəqqətidir** | Hələ qərar yoxdur (`Pending`) |

* Qərar tək yerdə: `ICertificateEligibilityPolicy`. Controller, UI və Excel yalnız bunu çağırır.
* İki növ sertifikatın fərqli QuestPDF şablonu olur (başlıq, mətn, rəng). Hər ikisinin unikal kodu və QR kodu var, hər ikisi `/verify/{code}` səhifəsində doğrulanır.
* Cohort-suz (`SelfPaced`) kurslarda köhnə qayda qalır: 100% tərəqqi ilə kurs tamamlama sertifikatı (`Course.DeliveryMode: SelfPaced | Cohort`).
* Sertifikat ləğv oluna bilər (Admin, səbəblə), ləğv olunmuş kod `/verify`-də «Ləğv edilib» göstərir.

---

## 4. Keyfiyyət Standartları (Hər Mərhələdə Yoxlanır)
* Hər endpoint: `[Authorize]` siyasəti, ownership yoxlanışı, FluentValidation, `Result<T>` → uyğun HTTP status, ProblemDetails.
* Siyahı endpoint-ləri pagination + sort + filter (`PagedResult<T>`, pageSize ≤ 100).
* Hər yeni cədvəl: Lazım olan indekslər, unikal məhdudiyyətlər, RowVersion, mənalı miqrasiya adı.
* Hər yeni qayda üçün unit test, hər yeni əsas axın üçün inteqrasiya testi.
* Scalar-da hər endpoint sənədləşdirilir.
* Yalnız lazımi sorğular (N+1 yoxdur, oxumada `AsNoTracking`, `Select` proyeksiya).
* Serilog structured log, həssas məlumat heç vaxt loga düşmür.
* UI: AZ/EN/RU açarları, dark mode, mobil uyğunluq, skeleton, toast, xəta mesajı.

---

## 5. Git Qaydaları
* **Branch-lar:** `main` (təhvil), `develop`, `feature/<mərhələ-modul>`, `fix/<mövzu>`. Hər mərhələ üçün ayrıca `feature/*` branch, bitəndə `develop`-a merge (PR təsviri hazırla).
* **Commit-lər:** Kiçik və mənalı, Conventional Commits (məs: `feat(attendance): add bulk upsert endpoint`).
* Mövcud tarixçəni yenidən yazma (rebase və force-push yoxdur).
* **Təsdiqdən əvvəl commit yoxdur, push yoxdur.**

---

## 6. Mərhələ Xəritəsi (10–30 Noyabr)
| № | Mərhələ | Tarix |
| :--- | :--- | :--- |
| **0** | Audit, .NET 10, mühəndislik təməli | 10–11 Noy |
| **1** | Google Auth + e-poçtla şifrəsiz giriş + token sistemi | 12–13 Noy |
| **2** | Akademik nüvə: Cohort, Cədvəl, Davamiyyət, Yekun bal, Sertifikat qərarı | 14–16 Noy |
| **3** | Assignment, qiymət daxiletməsi, iki növ sertifikat, Excel | 17–18 Noy |
| **4** | Elanlar, bildirişlər, Q&A | 19–20 Noy |
| **5** | Portal UI (API + JS): tələbə və müəllim | 21–23 Noy |
| **6** | Admin panel (MVC + API) | 24 Noy |
| **7** | Kataloq, qlobal AI vidcet, Pro, Jitsi (+ 7B ixtiyari) | 25–26 Noy (26 Noy axşamı feature freeze) |
| **8** | Stabilizasiya, test, seed data, Docker, CI | 27–28 Noy |
| **9** | Sənəd, Scalar, demo və müdafiə hazırlığı | 29–30 Noy |
