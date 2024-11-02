# Text Library

## How to Add or Edit a Language

- Open the `glossary.csv` file. Please avoid using Office Excel, as it may alter the file encoding. You can use https://edit-csv.net for editing instead.
- Insert the new language in the last column, filling in the first three rows with language information. `language_code-google` is used to automatically call Google Translate.
- Enter the corresponding terms for the new language.
- After completing this, run `conver_glossary_to_xaml.bat` to import the translated text into the APP source code. (Note: `conver_glossary_to_xaml.bat` requires Python support.)

## 如何新增或编辑语言

- 打开 `glossary.csv` 文件，请不要用 office excel 打开 csv，因为 excel 会修改文件的编码格式。可以使用 https://edit-csv.net 进行编辑。
- 将在最后一列插入要新增的语言，前三行填写语言信息，language_code-google 用于自动调用 Google 翻译。
- 填入词组在新语言中对应的单词
- 完成后执行 conver_glossary_to_xaml.bat，将翻译文本导入到 APP 源码中。（注意，conver_glossary_to_xaml.bat 需要 python 环境支持）