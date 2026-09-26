# PlateUp! Arabic glossary

The game is a co-op restaurant roguelite. A *run* is a restaurant that lives for as many
*days* as the players can survive; each day customers arrive, order, wait, eat and pay, and
one customer running out of patience ends the run. Between days the restaurant is rebuilt
from *blueprints* and improved with *upgrade cards*. Terms below are fixed across the whole
translation so a word always means the same mechanic.

## Structure of a run

| English | Arabic | Note |
|---|---|---|
| Run | جولة | one restaurant, from day 1 until it closes |
| Day | يوم | |
| Overtime Day | يوم إضافي | the extra shift after day 15 |
| Preparation | التحضير | the window before customers arrive |
| Practice Mode | وضع التدريب | rehearse without customers |
| Headquarters (HQ) | المقر | the hub you return to between runs |
| Rating | التصنيف | the restaurant's star level |
| Heat | الحرارة | difficulty modifier upward; keep the flame metaphor |
| Chill | البرودة | difficulty modifier downward |
| Seed | البذرة | |
| Floorplan / Layout | المخطط | the room shape |
| Setting | الطابع | the restaurant's locale (seaside, forest, ...) |
| Theme | السمة | a visual style unlock |
| Franchise | الامتياز | carrying cards into a new location |
| Experience (XP) | الخبرة | |
| Contract | العقد | |

## The service loop

| English | Arabic | Note |
|---|---|---|
| Customer | زبون | |
| Group | مجموعة | customers who arrive and must be served together |
| Patience | الصبر | the bar over a customer's head |
| Order | الطلب | |
| Serve / Deliver | يقدّم | put the food on the table |
| Mess | الفوضى | what customers leave behind |
| Table | الطاولة | |
| Queue | الطابور | |
| Earnings | الأرباح | |
| Tip | البقشيش | |

## Food

| English | Arabic | Note |
|---|---|---|
| Dish | الطبق | a menu option, not a plate |
| Plate | الصحن | the physical object |
| Starter | المقبّلات | |
| Main | الطبق الرئيسي | |
| Side | الطبق الجانبي | |
| Dessert | الحلوى | |
| Recipe | الوصفة | |
| Portion | الحصة | |
| Ingredient | المكوّن | |
| Flavour | النكهة | for cakes |

Named foods keep the name people order by: widely known dishes are transliterated
(كروسان, لازانيا) and ordinary ingredients are translated (سمك, بطاطس, جزر). A dish is
never renamed to a local equivalent - the picture on the plate has to still be the word.

## Kitchen

| English | Arabic | Note |
|---|---|---|
| Appliance | الجهاز | anything you buy and place |
| Blueprint | المخطط | the buyable card an appliance comes from |
| Counter | الطاولة | the plain surface you put things on |
| Hob | الموقد | |
| Sink | الحوض | |
| Bin | سلة المهملات | |
| Loading Bay | منطقة التفريغ | where parcels arrive |
| Parcel | الطرد | |
| Interact | التفاعل | the verb the control prompts use |
| Grab | الإمساك | |
| Chop | التقطيع | |
| Knead | العجن | |
| Cook | الطبخ | اطبخ as the imperative: the imperative of طها is اطهُ, unreadable once the build strips its damma |
| Clean | التنظيف | |
| Combine | الدمج | |
| Split | التقسيم | |

## Progression

| English | Arabic | Note |
|---|---|---|
| Card | البطاقة | |
| Upgrade card | بطاقة ترقية | |
| Reroll | إعادة السحب | |
| Skip | التخطي | |
| Scrap | التفكيك | trade cards for experience |
| Unlock | فتح | |
| Upgradable | قابل للترقية | the $upgradable$ marker on a blueprint |
| Enchantable | قابل للسحر | the $enchant$ marker |
| Research | البحث | the research desk |
| Greenhouse | البيت الزجاجي | |
| Garage | المرآب | |

## Style

- Modern Standard Arabic, second person masculine singular for instructions
  ("ضع البطاطس على الطاولة"), matching the English imperative.
- Keep sentences short. These strings sit on cards and floor labels with little room, and
  lines are broken by TextMesh Pro, which cannot re-wrap right-to-left text well.
- Short vowels and shadda may be written here, but the build strips them: TextMesh Pro does
  no mark positioning, so a mark lands on the neighbouring letter instead of its own. Never
  rely on a diacritic to carry a meaning the consonants do not - if a word is ambiguous
  without its vowels, choose a different word.
- Numbers, percentages and `{0}` placeholders stay in Latin digits, as the game prints them.
- `$icon$` tokens, `{{+...}}` colour spans and `<sprite>` tags are copied through untouched;
  only the words around them are translated.
