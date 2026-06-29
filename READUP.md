# DeployKit — دليل التحديث التلقائي

اضبط مشروعك عشان يستقبل التحديثات من **DeployKit Cloud** بثلاثة أسطر فقط.

---

## 1. إضافة المرجع للمشروع

**في ملف `.csproj`** لمشروعك:

```xml
<ItemGroup>
  <!-- إذا DeployKit جنب مشروعك -->
  <ProjectReference Include="..\DeployKit\DeployKit.Integration\DeployKit.Integration.csproj" />

  <!-- أو بعد ما تنزله من NuGet -->
  <!-- <PackageReference Include="DeployKit.Integration" Version="1.0.0" /> -->
</ItemGroup>
```

> المتطلب: المشروع لازم يكون `net8.0-windows` لأن SDK يستخدم WPF لنافذة التحديث.

---

## 2. تفعيل SDK (سطر واحد)

**في `App.xaml.cs`**:

```csharp
using DK = DeployKit.Integration.DeployKit;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DK.Configure("YOUR_API_KEY", "https://deploykit-api.onrender.com");
    }
}
```

`Configure` تسوي ثلاث أشياء تلقائياً:
1. تقرأ إصدار تطبيقك من الـ Assembly
2. تفحص السحابة في الخلفية
3. إذا فيه تحديث ← تظهر نافذة التحديث تلقائياً

---

## 3. ماذا يحدث عند وجود تحديث؟

```
تطبيقك شغال
    │
    ├── SDK يفحص السحابة (خلفية)
    │
    ├── وجد تحديث v1.0.1 → يظهر UpdateWindow
    │
    ├── ضغط "تحديث الآن" →
    │   1. SDK يحمل الحزمة من السحابة
    │   2. يشغل updater.exe
    │   3. ينهي تطبيقك
    │
    └── updater.exe →
        1. ينتظر تطبيقك يقفل
        2. يطبق الحزمة (يضيف / يعدل / يحذف)
        3. يحفظ نسخة احتياطية (للرجوع)
        4. يعيد تشغيل تطبيقك بالإصدار الجديد
```

---

## 4. الرجوع للإصدار السابق (Rollback)

إذا صار خطأ بعد التحديث، SDK يحفظ نسخة احتياطية تلقائياً:

```csharp
// تحقق من وجود نسخة احتياطية
var info = DK.GetRollbackInfo();
if (info != null)
    Console.WriteLine($"يمكن الرجوع من v{info.PreviousVersion}");

// الرجوع
var error = await DK.RollbackAsync();
if (string.IsNullOrEmpty(error))
    Console.WriteLine("تم الرجوع بنجاح!");
```

أو من نافذة التحديث: يظهر زر "رجوع للإصدار السابق" إذا وجدت نسخة احتياطية.

---

## 5. تشفير الحزم (AES-256)

اختياري — تشفير حزمة التحديث قبل رفعها:

```csharp
// توليد مفتاح
var key = DeployKit.Core.Services.EncryptionService.GenerateKey();
// حفظ المفتاح: key هو base64 string

// بناء حزمة مشفرة
var enc = new EncryptionService(key);
var builder = new PackageBuilder(enc);
await builder.BuildAsync(oldDir, newDir, comparison, "App", "1.0", "2.0", "update.dkup");
```

السحابة تتعامل مع الحزمة المشفرة كـ ZIP عادي. التطبيق يفك التشفير عند التطبيق:

```csharp
var applier = new PackageApplier(backupDir, new EncryptionService(key));
await applier.ApplyAsync("update.dkup", targetDir);
```

---

## 6. فحص يدوي (اختياري)

```csharp
DK.Configure("key", "url");

var result = await DK.CheckAsync();
if (result.HasUpdate)
    MessageBox.Show($"تحديث v{result.LatestVersion} متوفر!");
else
    MessageBox.Show("لا توجد تحديثات");
```

---

## 7. رفع تحديث جديد للسحابة

### عن طريق DeployKit.Gui (مرئي)
1. افتح `DeployKit.Gui.exe` → Build
2. اختر المجلد القديم + المجلد الجديد
3. اضغط **Build** → يبني حزمة الفروقات
4. املأ API Key + Cloud URL
5. اضغط **Upload**

### عن طريق API (للأتمتة)

```powershell
# سجل تطبيقك (مرة واحدة)
$reg = Invoke-RestMethod -Method Post "https://deploykit-api.onrender.com/v1/register?name=MyApp"
$key = $reg.appKey

# ارفع تحديث
$bytes = [System.IO.File]::ReadAllBytes("update.dkup")
$url = "https://deploykit-api.onrender.com/v1/upload?key=$key&from=1.0.0&to=1.1.0"
Invoke-RestMethod -Uri $url -Method Post -Body $bytes -ContentType "application/octet-stream"
```

```bash
curl -X POST "https://deploykit-api.onrender.com/v1/upload?key=KEY&from=1.0.0&to=1.1.0" \
  --data-binary "@update.dkup" -H "Content-Type: application/octet-stream"
```

---

## 8. لوحة التحكم (Admin Dashboard)

السحابة توفر واجهة إدارة ويب (RTL) على المسار `/`:

```
https://your-cloud.com/     ← لوحة التحكم
```

المصادقة: إضافة header `X-Admin-Key: admin` (أو القيمة اللي ضبطتها في `AdminKey`)

| المسار | الغرض |
|--------|-------|
| `/` | لوحة التحكم (HTML/JS) |
| `/v1/admin/apps` | عرض كل التطبيقات |
| `/v1/admin/apps/{key}` | تفاصيل تطبيق + حزمه |
| `/v1/admin/apps/{key}` DELETE | حذف تطبيق |
| `/v1/admin/packages/{id}` | تفاصيل حزمة |
| `/v1/admin/packages/{id}` DELETE | حذف حزمة |

---

## 9. API Reference

| المسار | الطريقة | الوصف |
|--------|---------|-------|
| `/v1/register?name=X` | POST | تسجيل تطبيق جديد ← يرجع `appKey` |
| `/v1/upload?key=X&from=X&to=X` | POST | رفع حزمة تحديث (raw body = ZIP) |
| `/v1/check?key=X&v=X.X.X` | GET | فحص وجود تحديث |
| `/v1/dl/{id}` | GET | تحميل حزمة التحديث |
| `/v1/admin/apps` | GET | قائمة التطبيقات (يتطلب AdminKey) |
| `/v1/admin/apps/{key}` | GET | تفاصيل تطبيق |
| `/v1/admin/apps/{key}` | DELETE | حذف تطبيق |
| `/v1/admin/packages/{id}` | GET | تفاصيل حزمة |
| `/v1/admin/packages/{id}` | DELETE | حذف حزمة |

---

## 10. هيكل حزمة التحديث

```
update.dkup (ZIP)
├── manifest.json       ← التغييرات (JSON)
├── new-file.dll        ← ملفات جديدة/معدلة
├── folder\file.dll     ← ملفات في مجلدات فرعية
├── patches\hash.patch  ← تصحيحات ثنائية
```

- **Deleted**: يحذف من مجلد التطبيق
- **Added**: ينشئ ملفات جديدة
- **Modified**: يستبدل أو يطبق patch
- **Unchanged**: يتجاهل

---

## 11. حل المشاكل

| المشكلة | الحل |
|---------|------|
| SDK لا يجد التحديث | تأكد من `Version` في `.csproj` يطابق الإصدار القديم في السحابة |
| خطأ `404` من السحابة | تأكد من Cloud URL، أو السحابة نائمة (Render free tier) |
| التطبيق لا يشتغل بعد التحديث | تأكد أن `updater.exe` موجود بجنب التطبيق |
| ما ظهرت نافذة التحديث | SDK يفحص مرة واحدة عند البداية؛ استخدم `CheckAsync()` يدوياً |
| خطأ في الرجوع | تأكد من وجود `rollback.json` في `%LOCALAPPDATA%/DeployKit/` |

---

> صنع بواسطة [DeployKit](https://github.com/dahakabdellah12/DeployKit) — MIT License
