# Giải pháp: Chatbot tìm sản phẩm không trả về kết quả

## Vấn đề

Khi user yêu cầu tìm sản phẩm theo giá (ví dụ "cho tôi 1 sản phẩm giá dưới 100000", "sản phẩm giá thấp nhất", "giá dưới 200k"), chatbot trả về **"không tìm thấy sản phẩm"** mặc dù trong database có sản phẩm phù hợp (như "Moc Khoa Mini" giá 35.000đ).

### Các trường hợp cụ thể

**Trường hợp 1:**
```json
Request: {"message": "cho tôi 1 sản phẩm giá dưới 100000"}
AI extract: {
  "needProductSearch": true,
  "maxPrice": 100000,
  "description": "...",
  ...
}
Response: "chưa có sản phẩm nào dưới 100.000đ"
```
→ Database có sản phẩm giá 35.000đ nhưng không tìm ra.

**Trường hợp 2:**
```json
Request: {"message": "cho tôi 1 sản phẩm giá thấp nhất"}
AI extract: {
  "needProductSearch": true,
  "sortPrice": "asc",
  "description": "...",
  ...
}
Response: "chưa tìm được sản phẩm nào với mức giá thấp nhất"
```

**Trường hợp 3:**
```json
Request: {"message": "cho tôi 1 sản phẩm giá dưới 200k"}
AI extract: {
  "needProductSearch": true,
  "maxPrice": 200000,
  "description": "tìm món rẻ nhất dưới 200k"
}
Response: "chưa có sản phẩm nào dưới 200k"
```

## Nguyên nhân (Root Cause)

Trong `ChatbotService.SearchProductsAsync` (dòng 253-259):

```csharp
if (!string.IsNullOrWhiteSpace(criteria.Description))
{
    var descKeywords = criteria.Description.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    query = query.Where(p =>
        descKeywords.Any(kw =>
            (p.Name + " " + (p.Description ?? "")).ToLowerInvariant().Contains(kw)));
}
```

**Vấn đề:**
- AI thường sinh `description` từ yêu cầu user như: `"tìm món rẻ nhất dưới 200k"`, `"sản phẩm giá thấp nhất"`, v.v.
- Những từ như **"món", "rẻ", "nhất", "dưới", "200k", "thấp"** là **metadata về giá/yêu cầu tìm kiếm**, không phải thuộc tính sản phẩm.
- Code đang tách từng từ và lọc: `query.Where(p => descKeywords.Any(kw => (p.Name + p.Description).Contains(kw)))`
- → Những sản phẩm có tên/mô tả kiểu "Moc Khoa Mini", "Gấu Bông", v.v. **không chứa** từ "rẻ", "thấp", "món" → bị lọc ra hết.
- → Kết quả cuối: `products.Count = 0` → chatbot trả lời "không có sản phẩm".

**Hiện trạng:**
- `minPrice`, `maxPrice`, `sortPrice` → **đã được xử lý riêng** (dòng 228-268) và hoạt động đúng.
- Nhưng đoạn `description` filter lại **phá hủy** kết quả đã lọc bằng giá.

## Giải pháp

### Option 1: Loại bỏ hoàn toàn filter theo `description` (Khuyến nghị)

**Lý do:**
- `description` thường chứa **metadata về yêu cầu tìm kiếm** (giá, sort), không phải đặc điểm sản phẩm.
- Các field khác (`products`, `attributes`, `minPrice`, `maxPrice`, `sortPrice`, `origin`) **đã đủ** để lọc.
- Việc tách từ đơn giản (`Split(' ')`) **dễ tạo false negative** với các từ như "món", "rẻ", "dưới", v.v.

**Thay đổi:**
```csharp
// Xóa hoặc comment đoạn 253-259 trong ChatbotService.cs:

// if (!string.IsNullOrWhiteSpace(criteria.Description))
// {
//     var descKeywords = criteria.Description.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
//     query = query.Where(p =>
//         descKeywords.Any(kw =>
//             (p.Name + " " + (p.Description ?? "")).ToLowerInvariant().Contains(kw)));
// }
```

**Kết quả:**
- Query chỉ lọc theo: `products`, `attributes`, `minPrice`, `maxPrice`, `origin` → chính xác hơn.
- `sortPrice` sắp xếp cuối cùng → đúng với ý định user.

---

### Option 2: Chỉ dùng `description` khi không có filter khác (Fallback)

Nếu muốn giữ `description` cho những trường hợp user hỏi mơ hồ (ví dụ: "quà gì đó ấm áp và sang trọng"), chỉ áp dụng khi **không có filter cụ thể** khác:

```csharp
// Chỉ filter theo description nếu KHÔNG có products, attributes, minPrice, maxPrice, origin
if (!string.IsNullOrWhiteSpace(criteria.Description) 
    && criteria.Products.Count == 0 
    && criteria.Attributes.Count == 0 
    && !criteria.MinPrice.HasValue 
    && !criteria.MaxPrice.HasValue 
    && criteria.Origin.Count == 0)
{
    var descKeywords = criteria.Description.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    query = query.Where(p =>
        descKeywords.Any(kw =>
            (p.Name + " " + (p.Description ?? "")).ToLowerInvariant().Contains(kw)));
}
```

**Kết quả:**
- Request với giá → không dùng `description` filter → tìm được sản phẩm đúng.
- Request mơ hồ kiểu "quà sang trọng" → vẫn dùng `description` để match.

---

### Option 3: Cải thiện AI prompt để không sinh `description` khi có filter giá

Thay đổi system prompt trong `ExtractKeywordsAsync` (dòng 107-163):

```
Luật description:
- Nếu khách chỉ hỏi về giá/tìm theo giá mà KHÔNG mô tả phong cách/loại sản phẩm cụ thể
  → để description = ""
- Ví dụ: "sản phẩm dưới 100k" → description = ""
- Ví dụ: "quà sang trọng dưới 500k" → description = "quà sang trọng"
```

**Ưu điểm:** AI tự nhận biết khi nào không cần `description`.  
**Nhược điểm:** Phụ thuộc độ chính xác AI, có thể vẫn sinh `description` đôi khi.

---

## Khuyến nghị

**→ Dùng Option 1: Xóa đoạn filter theo `description`**

Vì:
- Đơn giản, ổn định nhất.
- Các field khác đã đủ cho 90% trường hợp.
- Nếu sau này cần semantic search theo "phong cách" → dùng embedding/vector search, không phải split từ đơn giản.

**Nếu muốn giữ `description` cho edge case mơ hồ** → dùng **Option 2** (chỉ dùng khi không có filter nào khác).

---

## Code cần sửa

**File:** `Infrastructure/Core/ChatbotService.cs`

**Dòng:** 253-259

**Hành động:** Comment hoặc xóa đoạn filter theo `description`, hoặc áp dụng điều kiện fallback như Option 2.

---

## Test lại sau khi sửa

```json
Request: {"message": "cho tôi 1 sản phẩm giá dưới 100000"}
Expected: Trả về Moc Khoa Mini (35.000đ) trong productSuggestions

Request: {"message": "cho tôi 1 sản phẩm giá thấp nhất"}
Expected: Trả về sản phẩm có giá thấp nhất (sorted asc)

Request: {"message": "cho tôi 1 sản phẩm giá dưới 200k"}
Expected: Trả về danh sách sản phẩm có giá ≤ 200.000đ
```

---

## Bonus: Cải thiện thêm

Sau khi sửa xong, có thể cân nhắc:

1. **Thêm điều kiện `Inventory.Quantity > 0`** trong `SearchProductsAsync` để không gợi ý sản phẩm hết hàng.
2. **Tăng limit** từ `Take(5)` lên `Take(10)` nếu muốn đa dạng hơn (hoặc để client phân trang).
3. **Fallback khi không có kết quả:** Nếu lọc theo `products`/`attributes` ra 0 → thử lại query mềm hơn (chỉ lọc giá) trước khi báo "không tìm thấy".
