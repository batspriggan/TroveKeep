// Guards the class of bug that renders a whole page blank: a name the template binds that the
// <script setup> block never exposes. Vue does not fail the build for it, the view just throws on
// first render ("Property X was accessed during render but is not defined on instance"), so it is
// checked here by compiling the real components.
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { parse, compileScript, compileTemplate } from 'vue/compiler-sfc'

const SRC = new URL('../src', import.meta.url).pathname

function vueFiles(dir) {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry)
    if (statSync(path).isDirectory()) return vueFiles(path)
    return path.endsWith('.vue') ? [path] : []
  })
}

/**
 * Names the compiler reports as free in the template of a `<script setup>` SFC. Vue's binding
 * metadata already knows which identifiers the setup block provides, so anything the template
 * uses but the setup does not declare shows up as a `BindingTypes.SETUP_MAYBE_REF`/`_UNKNOWN`
 * reference with no match — easier to obtain from the compiled render function's unresolved list.
 */
function unboundTemplateNames(filename) {
  const source = readFileSync(filename, 'utf8')
  const { descriptor, errors } = parse(source, { filename })
  assert.deepEqual(errors, [], `${filename}: SFC parse errors`)

  if (!descriptor.scriptSetup) return []

  const script = compileScript(descriptor, { id: 'test' })
  // `bindings` maps every identifier the setup exposes (consts, refs, imports, props, types).
  const bindings = new Set(Object.keys(script.bindings ?? {}))

  const { errors: tplErrors } = compileTemplate({
    source: descriptor.template.content,
    filename,
    id: 'test',
    compilerOptions: {
      // Report every identifier that reaches render as a component/property lookup.
      isCustomElement: () => false,
    },
  })
  assert.deepEqual(tplErrors, [], `${filename}: template compile errors`)

  return { bindings, template: descriptor.template.content }
}

test('every name bound in a sticker template is exposed by its script setup', () => {
  const files = vueFiles(SRC).filter((f) => f.includes('stickers/'))
  assert.ok(files.length > 0, 'expected the sticker views to exist')

  for (const file of files) {
    const result = unboundTemplateNames(file)
    if (!result.bindings) continue
    const { bindings, template } = result

    // Root-level identifiers used in bindings (ignoring HTML tags, literals and local scope).
    // `title`/`placeholder`/`alt` carry prose, not expressions: only real bindings are scanned.
    const EXPRESSION_ATTR = /^(:|@|v-if$|v-else-if$|v-show$|v-model|v-for$)/
    const used = new Set()
    for (const match of template.matchAll(/(?:^|\s)([:@][A-Za-z-]+|v-[a-z-]+(?:\.[\w]+)*)="([^"]*)"/g)) {
      if (!EXPRESSION_ATTR.test(match[1])) continue
      // Drop string literals ('…') so words inside them are not read as identifiers, and the keys
      // of inline object bindings (:class="{ key: expr }") which are CSS class names, not bindings.
      const expression = match[2]
        .replace(/'[^']*'/g, "''")
        .replace(/`[^`]*`/g, "``")
        .replace(/\b([A-Za-z0-9_$]+)\s*:/g, '')
      for (const ident of expression.matchAll(/(?<![.\w$'])\b([a-z][A-Za-z0-9_]*)\b/g)) {
        used.add(ident[1])
      }
    }

    // Keywords, literals and Vue's built-ins are not bindings.
    const ignore = new Set([
      'true', 'false', 'null', 'undefined', 'in', 'of', 'new', 'typeof', 'instanceof',
      'if', 'else', 'return', 'await', 'function', 'const', 'let', 'var', 'delete',
      'number', 'string', 'value', 'target', 'checked', 'files', 'file', 'key', 'event',
    ])

    // `v-for` introduces its own scope: those names are the iterator, not missing bindings.
    const local = new Set()
    for (const match of template.matchAll(/v-for="\s*(?:\(?\s*([A-Za-z0-9_$]+)(?:\s*,\s*([A-Za-z0-9_$]+))?\s*\)?)\s+(?:in|of)\s/g)) {
      if (match[1]) local.add(match[1])
      if (match[2]) local.add(match[2])
    }

    const missing = [...used]
      .filter((name) => !ignore.has(name) && !local.has(name))
      .filter((name) => !bindings.has(name))

    assert.deepEqual(
      missing,
      [],
      `${relative(SRC, file)}: template uses names the script setup does not expose: ${missing.join(', ')}`,
    )
  }
})
