/**
 * Pragma LMS - Axtarış və Normallaşdırma Mühərriki (search.js)
 * Diakritiksiz axtarış, prefiks/substring uyğunluğu və 2-ci mərhələ fuzzy təklif ("Bunu mu demək istəyirsiniz?").
 */

(function (root, factory) {
    if (typeof define === 'function' && define.amd) {
        define([], factory);
    } else if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.PragmaSearch = factory();
    }
}(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    // Qısa texnoloji tokenlər dəqiq uyğunlaşdırılır, fuzzy edilmir
    const EXACT_TECH_TOKENS = new Set([
        'js', 'css', 'ui', 'ai', 'c', 'net', 'api', 'rag', 'llm', 'spa', 'ddd', 'cqrs', 'mvc', 'sql', 'c#'
    ]);

    // Azərbaycan hərflərinin ingilis/əsas qarşılığı
    const AZ_CHAR_MAP = {
        'ə': 'e', 'Ə': 'e',
        'ü': 'u', 'Ü': 'u',
        'ö': 'o', 'Ö': 'o',
        'ı': 'i', 'I': 'i',
        'i': 'i', 'İ': 'i',
        'ş': 's', 'Ş': 's',
        'ç': 'c', 'Ç': 'c',
        'ğ': 'g', 'Ğ': 'g'
    };

    /**
     * 1. NORMALLAŞDIRMA (diakritiksiz)
     */
    function normalize(text) {
        if (!text || typeof text !== 'string') return '';

        // "İ" və "I" problemi üçün əvvəlcə əl ilə xəritələ, sonra toLowerCase işlət
        let mapped = '';
        for (let i = 0; i < text.length; i++) {
            const ch = text[i];
            if (ch === 'İ') {
                mapped += 'i';
            } else if (ch === 'I') {
                mapped += 'i';
            } else {
                mapped += AZ_CHAR_MAP[ch] || ch;
            }
        }

        let lower = mapped.toLowerCase();

        // Qalan Azərbaycan hərflərini ingilis qarşılığına çevir: ə->e, ü->u, ö->o, ı->i, ş->s, ç->c, ğ->g
        let azCleaned = '';
        for (let i = 0; i < lower.length; i++) {
            const ch = lower[i];
            azCleaned += AZ_CHAR_MAP[ch] || ch;
        }

        // Qalan diakritikləri NFD normalizasiyası ilə sil
        return azCleaned.normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim();
    }

    /**
     * Durğu işarələrini boşluğa çevirir və tokenlərə bölür.
     * Məs: "Alpine.js" -> ["alpine", "js"]
     *      "Tailwind CSS & Reaktiv UI" -> ["tailwind", "css", "reaktiv", "ui"]
     */
    function tokenize(normalizedText) {
        if (!normalizedText) return [];
        const rawTokens = normalizedText.replace(/[^a-z0-9#]+/g, ' ').trim().split(/\s+/);
        return rawTokens.filter(t => t.length > 0);
    }

    /**
     * Levenshtein məsafəsi hesablama
     */
    function levenshtein(a, b) {
        if (a === b) return 0;
        if (!a.length) return b.length;
        if (!b.length) return a.length;

        const matrix = [];
        for (let i = 0; i <= b.length; i++) matrix[i] = [i];
        for (let j = 0; j <= a.length; j++) matrix[0][j] = j;

        for (let i = 1; i <= b.length; i++) {
            for (let j = 1; j <= a.length; j++) {
                if (b.charAt(i - 1) === a.charAt(j - 1)) {
                    matrix[i][j] = matrix[i - 1][j - 1];
                } else {
                    matrix[i][j] = Math.min(
                        matrix[i - 1][j - 1] + 1, // əvəzləmə
                        matrix[i][j - 1] + 1,     // əlavə
                        matrix[i - 1][j] + 1      // silmə
                    );
                }
            }
        }
        return matrix[b.length][a.length];
    }

    /**
     * Token uzunluğuna görə maksimum icazə verilən Levenshtein səhvi:
     * 1-4 hərf: max 1
     * 5-8 hərf: max 2
     * >8 hərf: max 3
     */
    function getMaxLevenshtein(len) {
        if (len <= 4) return 1;
        if (len <= 8) return 2;
        return 3;
    }

    /**
     * Subsequence uyğunluğu:
     * - Sorğu hərfləri kurs tokenində ardıcıllıqla tapılır (arada hərf buraxıla bilər)
     * - Tokenin ilk hərfi ilə başlamalıdır (sub[0] === str[0])
     * - Yalnız 2 və daha çox hərfli sorğu üçün
     * - Sorğu tokeninin uzunluğu kurs tokeninin uzunluğunun ən azı 40%-i olmalıdır
     */
    function isSubsequence(sub, str) {
        if (sub.length < 2) return false;
        if (sub.length < str.length * 0.4) return false;
        if (sub[0] !== str[0]) return false;

        let subIdx = 0;
        for (let i = 0; i < str.length && subIdx < sub.length; i++) {
            if (str[i] === sub[subIdx]) {
                subIdx++;
            }
        }
        return subIdx === sub.length;
    }

    const MULTI_LANG_METAS = [
        {
            match: function (c) {
                const s = (c.slug || '').toLowerCase();
                const t = (c.title || '').toLowerCase();
                return s.includes('csharp') || s.includes('onion') || t.includes('c#') || t.includes('onion');
            },
            titles: ['C# 14 & .NET 10 Modern Onion Architecture', 'C# 14 и .NET 10: Современная луковая архитектура (Onion)'],
            descs: [
                'Clean Architecture, DDD, CQRS principles, and enterprise LMS development with SQL Server.',
                'Clean Architecture, DDD, CQRS и разработка корпоративной системы управления обучением (LMS) на SQL Server.'
            ]
        },
        {
            match: function (c) {
                const s = (c.slug || '').toLowerCase();
                const t = (c.title || '').toLowerCase();
                return s.includes('frontend') || s.includes('alpine') || t.includes('frontend') || t.includes('alpine');
            },
            titles: ['Modern Frontend: Alpine.js, Tailwind CSS & Reactive UI', 'Современный фронтенд: Alpine.js, Tailwind CSS и реактивный UI'],
            descs: [
                'Modern SPA design, component architecture, reactive state management, and YouTube API integration.',
                'Современный дизайн SPA, компонентная архитектура, управление реактивным состоянием и интеграция с YouTube API.'
            ]
        },
        {
            match: function (c) {
                const s = (c.slug || '').toLowerCase();
                const t = (c.title || '').toLowerCase();
                return s.includes('gemini') || s.includes('generative') || t.includes('gemini') || t.includes('generative');
            },
            titles: ['Generative AI & LLM Integration: Google Gemini API', 'Generative AI и интеграция LLM: Google Gemini API', 'Генеративный ИИ и интеграция LLM: Google Gemini API'],
            descs: [
                'Google Gemini API, Socratic teaching methods, Prompt Engineering, and RAG architecture for LMS.',
                'Google Gemini API, Сократический метод обучения, Prompt Engineering и построение архитектуры RAG в LMS.',
                'Google Gemini API, Сократовские методы обучения, Prompt Engineering и построение RAG-системы в LMS.'
            ]
        },
        {
            match: function (c) {
                const s = (c.slug || '').toLowerCase();
                const t = (c.title || '').toLowerCase();
                return s.includes('clean') || s.includes('net-10') || t.includes('clean');
            },
            titles: ['.NET 10 & Clean Architecture', '.NET 10 и Clean Architecture'],
            descs: [
                'Enterprise backend development, Clean Architecture patterns, EF Core 10, and high performance APIs.',
                'Корпоративная разработка бэкенда, паттерны Clean Architecture, EF Core 10 и высокопроизводительные API.'
            ]
        },
        {
            match: function (c) {
                const s = (c.slug || '').toLowerCase();
                const t = (c.title || '').toLowerCase();
                return s.includes('devops') || s.includes('lkjhgfd') || t.includes('devops');
            },
            titles: ['DevOps & Cloud Infrastructure Masterclass', 'Мастер-класс по DevOps и облачной инфраструктуре', 'DevOps və Bulud İnfrastrukturu Ustad Dərsi'],
            descs: [
                'Containerization with Docker, Kubernetes orchestration, CI/CD automation pipelines, and infrastructure as code.',
                'Контейнеризация с Docker, оркестрация в Kubernetes, CI/CD пайплайны и инфраструктура как код.',
                'Docker ilə konteynerləşdirmə, Kubernetes orkestrasiyası, CI/CD avtomatlaşdırma boru xətləri və infrastruktur kod kimi.'
            ]
        }
    ];

    /**
     * Kurs obyekti üçün normallaşmış sahələri əvvəlcədən 1 dəfə hesablayır (cache).
     */
    function prepareCourse(course) {
        if (!course) return null;

        const normTitle = normalize(course.title || '');
        const normDesc = normalize(course.shortDescription || course.description || '');
        const normCategory = normalize(course.categoryName || '');
        const normInstructor = normalize(course.instructorFullName || '');

        const titleTokens = tokenize(normTitle);
        const descTokens = tokenize(normDesc);
        const categoryTokens = tokenize(normCategory);
        const instructorTokens = tokenize(normInstructor);

        // Çoxdillilik (EN / RU) tokenlərinin inteqrasiyası
        const extraTitleTokens = [];
        const extraDescTokens = [];
        for (let i = 0; i < MULTI_LANG_METAS.length; i++) {
            const meta = MULTI_LANG_METAS[i];
            if (meta.match(course)) {
                meta.titles.forEach(t => extraTitleTokens.push(...tokenize(normalize(t))));
                meta.descs.forEach(d => extraDescTokens.push(...tokenize(normalize(d))));
                break;
            }
        }

        if (Array.isArray(course.translations)) {
            for (const tr of course.translations) {
                if (tr.title) extraTitleTokens.push(...tokenize(normalize(tr.title)));
                if (tr.shortDescription) extraDescTokens.push(...tokenize(normalize(tr.shortDescription)));
                if (tr.description) extraDescTokens.push(...tokenize(normalize(tr.description)));
            }
        }

        const mergedTitleTokens = Array.from(new Set([...titleTokens, ...extraTitleTokens]));
        const mergedDescTokens = Array.from(new Set([...descTokens, ...extraDescTokens]));

        // Bütün unikal tokenlər
        const allTokens = Array.from(new Set([
            ...mergedTitleTokens,
            ...mergedDescTokens,
            ...categoryTokens,
            ...instructorTokens
        ]));

        return {
            ...course,
            _norm: {
                title: normTitle,
                desc: normDesc,
                titleTokens: mergedTitleTokens,
                descTokens: mergedDescTokens,
                allTokens
            }
        };
    }

    function prepareCourses(courses) {
        if (!Array.isArray(courses)) return [];
        return courses.map(prepareCourse);
    }

    /**
     * Əsas Axtarış Funksiyası:
     * @param {Array} preparedCourses - prepareCourse ilə hazırlanmış kurslar siyahısı
     * @param {string} rawQuery - İstifadəçinin daxil etdiyi axtarış sətri
     * @returns {{ results: Array, suggestion: object|null }}
     */
    function search(preparedCourses, rawQuery) {
        if (!Array.isArray(preparedCourses) || preparedCourses.length === 0) {
            return { results: [], suggestion: null };
        }

        const trimmed = (rawQuery || '').trim();

        // Sorğu boşdursa bütün kataloq göstərilir
        if (!trimmed) {
            return { results: preparedCourses, suggestion: null };
        }

        const normQuery = normalize(trimmed);
        const queryTokens = tokenize(normQuery);

        // Tək hərfli sorğular: bütün kataloq göstərilir, təklif çıxarılmır
        if (trimmed.length === 1 || queryTokens.length === 0) {
            return { results: preparedCourses, suggestion: null };
        }

        // ==========================================
        // 1-Cİ MƏRHƏLƏ: BİRBAŞA NƏTİCƏ
        // ==========================================
        // Sorğunun hər tokeni kursun tokenlərindən hər hansı birinin prefiksi və ya içində substring olarsa.
        // Çoxsözlü sorğuda bütün tokenlər uyğun gəlməlidir (AND).
        const directMatches = [];

        for (const course of preparedCourses) {
            const norm = course._norm;
            if (!norm) continue;

            let allTokensMatch = true;
            let titleMatchScore = 0;
            let descMatchScore = 0;

            for (const qt of queryTokens) {
                // Başlıqda axtarış
                const inTitle = norm.titleTokens.some(tt => {
                    if (tt === qt) {
                        titleMatchScore += 10;
                        return true;
                    }
                    if (tt.startsWith(qt)) {
                        titleMatchScore += 5;
                        return true;
                    }
                    if (tt.includes(qt)) {
                        titleMatchScore += 3;
                        return true;
                    }
                    return false;
                });

                // Təsvirdə / digər sahələrdə axtarış
                const inOther = norm.allTokens.some(at => {
                    if (at === qt) {
                        descMatchScore += 3;
                        return true;
                    }
                    if (at.startsWith(qt)) {
                        descMatchScore += 2;
                        return true;
                    }
                    if (at.includes(qt)) {
                        descMatchScore += 1;
                        return true;
                    }
                    return false;
                });

                if (!inTitle && !inOther) {
                    allTokensMatch = false;
                    break;
                }
            }

            if (allTokensMatch) {
                // Başlıqda uyğunluq təsvirdə uyğunluqdan yuxarı sıralanır
                const totalScore = (titleMatchScore * 3) + descMatchScore;
                directMatches.push({ course, score: totalScore });
            }
        }

        if (directMatches.length > 0) {
            // Yüksək skordan aşağıya sırala
            directMatches.sort((a, b) => b.score - a.score);
            return {
                results: directMatches.map(m => m.course),
                suggestion: null
            };
        }

        // ==========================================
        // 2-Cİ MƏRHƏLƏ: "BUNU MU DEMƏK İSTƏYİRSİNİZ?"
        // ==========================================
        // Yalnız 1-ci mərhələdə heç nə tapılmayanda işə düşür.
        let bestCourseCandidate = null;
        let highestCourseScore = 0;

        for (const course of preparedCourses) {
            const norm = course._norm;
            if (!norm) continue;

            let matchedTokensCount = 0;
            let totalTokensScore = 0;

            for (const qt of queryTokens) {
                let bestTokenScore = 0;
                const isShortTech = EXACT_TECH_TOKENS.has(qt);

                for (const ct of norm.allTokens) {
                    // Dəqiq bərabərlik
                    if (qt === ct) {
                        bestTokenScore = Math.max(bestTokenScore, 1.0);
                        break;
                    }

                    // Qısa texnoloji tokenlər ("js", "css", "ui") dəqiq uyğunlaşdırılır, fuzzy edilmir!
                    if (isShortTech) {
                        continue;
                    }

                    // a) Levenshtein məsafəsi
                    const maxLen = Math.max(qt.length, ct.length);
                    const maxAllowedErr = getMaxLevenshtein(maxLen);
                    const dist = levenshtein(qt, ct);

                    if (dist <= maxAllowedErr) {
                        const levScore = 1.0 - (dist / (maxLen + 1));
                        if (levScore > bestTokenScore) {
                            bestTokenScore = levScore;
                        }
                    }

                    // b) Subsequence uyğunluğu
                    if (isSubsequence(qt, ct)) {
                        const subScore = 0.6 + 0.35 * (qt.length / ct.length);
                        if (subScore > bestTokenScore) {
                            bestTokenScore = subScore;
                        }
                    }
                }

                // Əgər token üçün kifayət qədər yüksək uyğunluq tapıldısa
                if (bestTokenScore >= 0.5) {
                    matchedTokensCount++;
                    totalTokensScore += bestTokenScore;
                }
            }

            // Çoxsözlü sorğuda tokenlərin ən azı yarısı uyğun gəlməlidir
            const requiredMatches = Math.ceil(queryTokens.length / 2);
            if (matchedTokensCount >= requiredMatches && queryTokens.length > 0) {
                const avgScore = totalTokensScore / queryTokens.length;
                if (avgScore > highestCourseScore && avgScore >= 0.45) {
                    highestCourseScore = avgScore;
                    bestCourseCandidate = course;
                }
            }
        }

        return {
            results: [],
            suggestion: bestCourseCandidate
        };
    }

    return {
        normalize,
        tokenize,
        levenshtein,
        isSubsequence,
        prepareCourse,
        prepareCourses,
        search
    };
}));
