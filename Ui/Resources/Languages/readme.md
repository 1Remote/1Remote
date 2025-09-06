# Text Library

## How to Add or Edit a Language

Text resources for multilingual support can be found in `Ui/Resources/Languages`.

Each language has its own CSV file in the `glossary/` directory. The `*.xaml` and `*.cs` files are generated from these CSV files and should not be edited directly.

### To Add a New Language

1. **Copy the English template**: Make a copy of `glossary/en-us.csv` and rename it to the appropriate language code (e.g., `es-es.csv` for Spanish, `ko-kr.csv` for Korean).

2. **Edit the language metadata**: Open the new CSV file and update the first 4 rows:
   - Row 1: Change `en-us` to your language code
   - Row 2: Set the ISO language code (e.g., `es-ES`, `ko-KR`)
   - Row 3: Set the Google Translate language code (e.g., `es`, `ko`)
   - Row 4: Set the display name (e.g., `Español (es-ES)`, `한국어 (ko-KR)`)

3. **Translate the content**: Edit the translations in the second column. You can use any text editor, but be careful not to change the encoding. Avoid using Office Excel. You can use https://edit-csv.net to edit it.

4. **Generate XAML files**: Run `conver_glossary_to_xaml.py` to generate the `*.xaml` and `LanguageList.cs` files.

### To Edit an Existing Language

Simply edit the corresponding CSV file in the `glossary/` directory and run `conver_glossary_to_xaml.py`.

> [!TIP]
> When editing CSV files, note the following:
>
> - The delimiter is a semicolon `;`
> - The line ending character is LF
> - To include semicolons or line breaks in text, enclose the entire text in double quotation marks
> - To include double quotation marks in text, enclose the entire text in double quotation marks, where the double quotation marks are represented by `""`
>
> Terms left blank in the CSV files will be automatically filled using Google Translate if available, and cached in `glossary_translated_by_google/` directory.

> [!NOTE]
> `conver_glossary_to_xaml.py` requires Python support.
> The first run may fail, so try it again.
>
> To access Google Translate, you may need to edit `conver_glossary_to_xaml.py`.
> For example, to remove proxy:
>
> ```python
>  # http_proxy = SyncHTTPProxy((b'http', b'127.0.0.1', 1080, b''))
>  # proxies = {'http': http_proxy, 'https': http_proxy}
>  # translator = Translator(proxies=proxies)
>  translator = Translator()
> ```

## 如何新增或编辑语言

### 新增语言

1. **复制英文模板**: 将 `glossary/en-us.csv` 文件复制一份，重命名为对应语言的代码（如西班牙语为 `es-es.csv`，韩语为 `ko-kr.csv`）。

2. **编辑语言信息**: 打开新创建的CSV文件，修改前4行的语言信息：
   - 第1行：将 `en-us` 改为你的语言代码
   - 第2行：设置ISO语言代码（如 `es-ES`，`ko-KR`）
   - 第3行：设置Google翻译语言代码（如 `es`，`ko`）
   - 第4行：设置显示名称（如 `Español (es-ES)`，`한국어 (ko-KR)`）

3. **翻译内容**: 编辑第二列中的翻译文本。请不要用Office Excel打开CSV，因为Excel会修改文件的编码格式。可以使用 <https://edit-csv.net> 进行编辑。

4. **生成XAML文件**: 运行 `conver_glossary_to_xaml.py`，将翻译文本导入到APP源码中。

### 编辑现有语言

直接编辑 `glossary/` 目录下对应的CSV文件，然后运行 `conver_glossary_to_xaml.py`。

> [!TIP]
> CSV文件中留空的词条将通过Google翻译自动填充，并缓存在 `glossary_translated_by_google/` 目录中。

> [!NOTE]
> `conver_glossary_to_xaml.py` 需要Python环境支持。
