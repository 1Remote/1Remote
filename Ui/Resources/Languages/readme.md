# Text Library

## How to Add or Edit a Language

Text resources for multilingual support can be found in `Ui/Resources/Languages`.

You should only edit `glossary.csv`. The `*.xaml` and `*.cs` files are generated from this and should not be edited directly.

You can use the tool of your choice to edit `glossary.csv`, but be careful not to change the encoding, so avoid using Office Excel and so on. You can use https://edit-csv.net to edit it.

> [!TIP]
> When editing the CSV file with a plain text editor or similar, note the following - 
> - The delimiter of this CSV file is a semicolon `;`.
> - The line ending character is LF.
> - To include semicolons or line breaks in the text, enclose the entire text in double quotation marks.
> - To include double quotation marks in text, enclose the entire text in double quotation marks, where the double quotation marks are represented by `""`.

Each column, except the first (leftmost), represents terms for the specific language. The first column is the key. You can find the language by looking in the row where the key is 'language_name' or 'key' (probably in the first four rows). To add a new language, add a new column to the right.

After editing the CSV file, running `conver_glossary_to_xaml.bat` will generate new `*.xaml` and `LanguageList.cs` files.
Terms left blank in `glossary.csv` will be filled in from `glossary_translated_by_google.csv`.
If it is also blank in there, Google Translate will be called using `language_code-google` and the result is stored in `glossary_translated_by_google.csv`.

> [!NOTE]
> `conver_glossary_to_xaml.bat` requires Python support.
> The first run may fail, so try it again.
> 
> To access Google Translate, you may need to edit `glossary_maker.py`.
> For example, to remove proxy:
> ```
>  # http_proxy = SyncHTTPProxy((b'http', b'127.0.0.1', 1080, b''))
>  # proxies = {'http': http_proxy, 'https': http_proxy}
>  # translator = Translator(proxies=proxies)
>  translator = Translator()
> ```

## 如何新增或编辑语言

- 打开 `glossary.csv` 文件，请不要用 office excel 打开 csv，因为 excel 会修改文件的编码格式。可以使用 https://edit-csv.net 进行编辑。
- 将在最后一列插入要新增的语言，前三行填写语言信息，language_code-google 用于自动调用 Google 翻译。
- 填入词组在新语言中对应的单词
- 完成后执行 conver_glossary_to_xaml.bat，将翻译文本导入到 APP 源码中。（注意，conver_glossary_to_xaml.bat 需要 python 环境支持）