#!/usr/bin/env python3
"""Builds src/locale/messages.en.xlf from the extracted messages.xlf and tools/translations.en.json.

Placeholders are written as {ID} in the JSON and replaced by the exact <x/> element of the source unit, so a
translation can reorder them but never corrupt them. Fails loudly on a missing translation or an unknown placeholder.
Run after `ng extract-i18n --output-path src/locale`.
"""
import copy, json, re, sys
import xml.etree.ElementTree as ET

NS = 'urn:oasis:names:tc:xliff:document:1.2'
ET.register_namespace('', NS)
tree = ET.parse('src/locale/messages.xlf')
translations = json.load(open('tools/translations.en.json', encoding='utf-8'))
tree.getroot().find(f'{{{NS}}}file').set('target-language', 'en')

missing = []
for unit in tree.getroot().iter(f'{{{NS}}}trans-unit'):
    uid = unit.get('id')
    source = unit.find(f'{{{NS}}}source')
    if uid not in translations:
        missing.append(uid)
        continue
    placeholders = {child.get('id'): child for child in source}
    target = ET.SubElement(unit, f'{{{NS}}}target')
    pieces = re.split(r'\{([A-Za-z0-9_]+)\}', translations[uid])
    target.text = pieces[0]
    last = None
    for index in range(1, len(pieces), 2):
        name, text = pieces[index], pieces[index + 1]
        if name not in placeholders:
            sys.exit(f'{uid}: unknown placeholder {{{name}}}')
        element = copy.deepcopy(placeholders[name])
        element.tail = text
        target.append(element)
    # Keep <target> right after <source> as the XLIFF schema expects.
    unit.remove(target)
    unit.insert(list(unit).index(source) + 1, target)

if missing:
    sys.exit('Missing English translations: ' + ', '.join(missing))
tree.write('src/locale/messages.en.xlf', encoding='UTF-8', xml_declaration=True)
print(f"messages.en.xlf written ({len(translations)} translations)")
