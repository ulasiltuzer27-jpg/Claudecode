// Ceviri. Anahtarlar duz ("menu.continue"); deger dizi olabilir (diyaloglar).
import tr from '../i18n/tr.json';
import en from '../i18n/en.json';

const DICTS = { tr, en };
let lang = 'tr';
const listeners = new Set();

export function detectLang() {
  const nav = (typeof navigator !== 'undefined' && navigator.language) || 'en';
  return nav.toLowerCase().startsWith('tr') ? 'tr' : 'en';
}

export function setLang(l) {
  lang = DICTS[l] ? l : 'en';
  if (typeof document !== 'undefined') document.documentElement.lang = lang;
  for (const fn of listeners) fn(lang);
}

export function getLang() {
  return lang;
}

export function onLangChange(fn) {
  listeners.add(fn);
}

export function has(key) {
  return DICTS[lang][key] !== undefined || DICTS.en[key] !== undefined;
}

export function t(key, params) {
  let v = DICTS[lang][key];
  if (v === undefined) v = DICTS.en[key];
  if (v === undefined) return key;
  if (params) {
    const fill = (s) => s.replace(/\{(\w+)\}/g, (_, k) => (params[k] !== undefined ? params[k] : `{${k}}`));
    return Array.isArray(v) ? v.map(fill) : fill(v);
  }
  return v;
}

export const LANGS = Object.keys(DICTS);
