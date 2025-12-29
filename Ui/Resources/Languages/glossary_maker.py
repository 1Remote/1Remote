#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Enhanced Language Manager with Google Translate support
"""

import code
import csv
import os
import sys
from googletrans import Translator
from httpcore import SyncHTTPProxy

Use_proxy_for_google_translator = False
#Use_proxy_for_google_translator = True

Special_Marks_in_XAML_Content = ["&", "<", ">", "\r", "\n"]
Special_Characters_in_XAML_Content = ["&amp;", "&lt;", "&gt;", "\\r", "\\n"]
Forbidden_Characters_in_XAML_Key = ['"', "'", *Special_Marks_in_XAML_Content]
Forbidden_Characters_in_XAML_Key_Values = ['&quot;', '&apos;', *Special_Characters_in_XAML_Content]

def Characters_to_Special_Marks_in_XAML(string: str) -> str:
    for i in range(len(Special_Marks_in_XAML_Content)):
        string = string.replace(Special_Marks_in_XAML_Content[i], Special_Characters_in_XAML_Content[i])
    return string

def Forbidden_Characters_in_XAML_Key_convert(string: str) -> str:
    for i in range(len(Forbidden_Characters_in_XAML_Key)):
        string = string.replace(Forbidden_Characters_in_XAML_Key[i], Forbidden_Characters_in_XAML_Key_Values[i])
    return string

def load_language_csv(csv_path):
    """Load a single language CSV file"""
    with open(csv_path, mode='r', encoding='utf-8') as f:
        reader = csv.reader(f, delimiter=";")
        lines = list(reader)

    if len(lines) < 4:
        raise ValueError(f"Invalid language file format: {csv_path}")

    lang_data = {
        'key': lines[0][1] if len(lines[0]) > 1 else "",
        'language_code-ISO': lines[1][1] if len(lines[1]) > 1 else "",
        'language_code-google': lines[2][1] if len(lines[2]) > 1 else "",
        'language_name': lines[3][1] if len(lines[3]) > 1 else "",
        'translations': {}
    }

    # Extract translations
    for i in range(4, len(lines)):
        if len(lines[i]) >= 2:
            key = lines[i][0]
            translation = lines[i][1]
            lang_data['translations'][key] = translation

    return lang_data

def save_language_csv(csv_path, lang_data):
    """Save language data to CSV file"""
    with open(csv_path, 'w', encoding='utf-8', newline='') as f:
        writer = csv.writer(f, delimiter=";", lineterminator='\n')

        # Write header rows
        writer.writerow(['key', lang_data['key']])
        writer.writerow(['language_code-ISO', lang_data['language_code-ISO']])
        writer.writerow(['language_code-google', lang_data['language_code-google']])
        writer.writerow(['language_name', lang_data['language_name']])

        # Write translations
        for key, translation in lang_data['translations'].items():
            writer.writerow([key, translation])

def load_translation_cache(lang_code):
    """Load translation cache for a specific language"""
    cache_file = f"glossary_translated_by_google/{lang_code}_translated_by_google.csv"
    cache = {}

    if os.path.exists(cache_file):
        try:
            with open(cache_file, mode='r', encoding='utf-8') as f:
                reader = csv.reader(f, delimiter=";")
                lines = list(reader)

                # Skip header rows and process translations
                for i in range(4, len(lines)):
                    if len(lines[i]) >= 2:
                        key = lines[i][0]
                        translation = lines[i][1]
                        if translation:  # Only store non-empty translations
                            cache[key] = translation
        except Exception as e:
            print(f"Warning: Could not load translation cache {cache_file}: {e}")

    return cache

def save_translation_cache(lang_code, iso_code, google_code, language_name, cache):
    """Save translation cache for a specific language"""
    os.makedirs("glossary_translated_by_google", exist_ok=True)
    cache_file = f"glossary_translated_by_google/{lang_code}_translated_by_google.csv"

    try:
        with open(cache_file, 'w', encoding='utf-8', newline='') as f:
            writer = csv.writer(f, delimiter=";", lineterminator='\n')

            # Write header rows
            writer.writerow(['key', lang_code])
            writer.writerow(['language_code-ISO', iso_code])
            writer.writerow(['language_code-google', google_code])
            writer.writerow(['language_name', language_name])

            # Write translations
            for key, translation in cache.items():
                writer.writerow([key, translation])

        print(f"Saved translation cache: {cache_file}")
    except Exception as e:
        print(f"Error saving translation cache {cache_file}: {e}")

def translate_text(translator, text, target_lang_code):
    """Translate text using Google Translate API"""
    try:
        result = translator.translate(text, dest=target_lang_code)
        return result.text
    except:
        # If failed, you can add proxy configuration here if needed
        # For now, just return None
        return None

def generate_xaml_files():
    if Use_proxy_for_google_translator:
        http_proxy = SyncHTTPProxy((b'http', b'127.0.0.1', 1080, b''))
        proxies = {'http': http_proxy, 'https': http_proxy}
        translator = Translator(proxies=proxies)
    else:
        translator = Translator()

    """Generate XAML files from CSV files in glossary directory with auto-translation"""
    print("Enhanced Language Manager - Generating XAML files...")
    print("=" * 50)

    glossary_dir = 'glossary'
    if not os.path.exists(glossary_dir):
        print(f"Error: {glossary_dir} directory not found!")
        return

    # Load English as reference
    english_file = os.path.join(glossary_dir, 'en-us.csv')
    if not os.path.exists(english_file):
        print(f"Error: English template not found: {english_file}")
        return

    english_data = load_language_csv(english_file)

    csv_files = [f for f in os.listdir(glossary_dir) if f.endswith('.csv')]
    if not csv_files:
        print(f"Error: No CSV files found in {glossary_dir}!")
        return

    file_list = []

    for csv_file in csv_files:
        lang_code = csv_file[:-4]  # Remove .csv extension
        csv_path = os.path.join(glossary_dir, csv_file)

        try:
            # Load language data
            lang_data = load_language_csv(csv_path)
            print(f"Processing: {lang_code} ({lang_data['language_name']})")

            # Load translation cache
            translation_cache = load_translation_cache(lang_code)
            cache_updated = False
            lang_updated = False
            # Check for missing keys (use English as reference)
            for key, english_text in english_data['translations'].items():
                if key not in lang_data['translations']:
                    # Missing key, add it (empty translation to be filled manually)
                    lang_data['translations'][key] = ""
                    lang_updated = True
                    print(f"  Added missing key: {key}")

            # Create a copy for XAML generation that includes cached/translated content
            xaml_data = lang_data.copy()
            xaml_data['translations'] = lang_data['translations'].copy()            # Process empty translations for XAML generation
            if lang_code != 'en-us':  # Skip English itself
                for key, translation in xaml_data['translations'].items():
                    if not translation:  # Empty translation
                        # First check cache
                        if key in translation_cache:
                            xaml_data['translations'][key] = translation_cache[key]
                            print(f"  Used cached translation for {key}: {translation_cache[key]}")

                        # If still empty and we have English text, try Google Translate
                        elif key in english_data['translations'] and english_data['translations'][key]:
                            english_text = english_data['translations'][key]
                            xaml_data['translations'][key] = english_text
                            translated = translate_text(translator, english_text, lang_data['language_code-google'])
                            if translated:
                                # Store in memory for XAML generation but don't save to CSV
                                xaml_data['translations'][key] = translated
                                translation_cache[key] = translated
                                cache_updated = True
                                print(f"  Translated {key}: {english_text} -> {translated}")
                                print(f"    (Translation stored in cache only, not in CSV)")

            # Save updated language file if changed
            if lang_updated:
                save_language_csv(csv_path, lang_data)
                print(f"  Updated language file: {csv_path}")
            # Save updated cache if changed
            if cache_updated:
                save_translation_cache(
                    lang_data['key'],
                    lang_data['language_code-ISO'],
                    lang_data['language_code-google'],
                    lang_data['language_name'],
                    translation_cache
                )

            # Generate XAML file
            xaml_file_name = (lang_code + '.xaml').lower()

            with open(xaml_file_name, 'w', encoding='utf-8', newline='') as f:
                f.write('<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:s="clr-namespace:System;assembly=mscorlib" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">\r\n')
                # Add language_name if not in translations
                key = xaml_data['key']
                code_iso = xaml_data['language_code-ISO']
                code_google = xaml_data['language_code-google']
                language_name = xaml_data['language_name']
                # write language codes
                f.write(f'    <s:String x:Key="key">{key}</s:String>\r\n')
                f.write(f'    <s:String x:Key="language_code-ISO">{code_iso}</s:String>\r\n')
                f.write(f'    <s:String x:Key="language_code-google">{code_google}</s:String>\r\n')
                f.write(f'    <s:String x:Key="language_name">{language_name}</s:String>\r\n')
                # Write all translations (including cached/translated ones)
                for key, translation in xaml_data['translations'].items():
                    safe_key = Forbidden_Characters_in_XAML_Key_convert(key)
                    safe_translation = Characters_to_Special_Marks_in_XAML(translation)
                    f.write(f'    <s:String x:Key="{safe_key}">{safe_translation}</s:String>\r\n')
                f.write('</ResourceDictionary>')

            file_list.append(xaml_file_name)
            print(f"Generated: {xaml_file_name}")

        except Exception as e:
            print(f"Error processing {csv_file}: {e}")

    # Generate LanguagesList.cs
    if file_list:
        with open("LanguagesList.cs", 'w', encoding='utf-8', newline='\r\n') as f:
            files_str = '",\n        "'.join(file_list)
            f.write(f'''
public static class LanguagesResources
{{
    public static readonly string[] Files = new string[]
    {{
        "{files_str}"
    }};
}}
''')
        print("Generated: LanguagesList.cs")

    print(f"\nCompleted! Generated {len(file_list)} XAML files.")

if __name__ == '__main__':
    generate_xaml_files()
