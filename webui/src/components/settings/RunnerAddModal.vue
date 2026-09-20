<script setup>
/**
 * 添加运行器模态（自 RunnerGroup.vue 拆出）：对齐 WPF CmdAddRunner 的 InputBox 流程——
 * 只输入名称（非空 + 协议内唯一），确认后由父建默认值运行器并选中新卡（下拉指向 +
 * 滚动入视野 + 短暂高亮）；exe 路径/启动参数等在卡片内继续配置。
 * 名称草稿与校验归本组件（打开即重置，上次未提交的草稿不带入新会话）；确认经
 * save 事件上抛 trimmed 名称（按钮 disabled 与回车守卫同源：校验未过不动作）。
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { onFormEnter } from '../../utils/formEnter'

const props = defineProps({
  /** 模态显隐（v-model:show） */
  show: { type: Boolean, default: false },
  /** 当前协议内已有的 runner 名单（唯一性校验域） */
  existingNames: { type: Array, default: () => [] },
})
const emit = defineEmits(['update:show', 'save'])
const { t } = useI18n()

const name = ref('')

// 打开即重置：上次未提交的草稿不带入新会话
watch(
  () => props.show,
  (v) => {
    if (v) name.value = ''
  }
)

// 名称校验 = WPF CmdAddRunner 的 InputBox 规则：非空 + 协议内唯一
const nameError = computed(() => {
  const n = name.value.trim()
  if (!n) return t('settings.r.nameRequired')
  if (props.existingNames.includes(n)) return t('settings.r.nameExists', { name: n })
  return ''
})
const valid = computed(() => !nameError.value)

function save() {
  if (!valid.value) return
  emit('save', name.value.trim())
}

// 模态表单回车=保存：共通语义见 utils/formEnter.js（与保存按钮同守卫：名称校验未过不动作）
</script>

<template>
  <n-modal
    :show="show"
    preset="card"
    :title="t('settings.r.addTitle')"
    :bordered="false"
    :style="{ width: 'min(520px, 92vw)' }"
    role="dialog"
    aria-modal="true"
    @update:show="emit('update:show', $event)"
  >
    <div class="form" @keydown="onFormEnter($event, save)">
      <div class="f-row">
        <label>{{ t('editor.f.Name') }}</label>
        <div>
          <n-input
            size="small"
            v-model:value="name"
            :status="nameError ? 'error' : undefined"
            :input-props="{ spellcheck: false }"
          />
          <p v-if="nameError" class="f-err">{{ nameError }}</p>
        </div>
      </div>
    </div>
    <template #footer>
      <div class="modal-actions">
        <n-button size="small" @click="emit('update:show', false)">{{ t('editor.cancel') }}</n-button>
        <n-button size="small" type="primary" :disabled="!valid" @click="save">
          {{ t('editor.save') }}
        </n-button>
      </div>
    </template>
  </n-modal>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.form .f-row {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
}
.form .f-row label {
  font-size: var(--fs-body);
  color: var(--text-2);
}
.f-err {
  margin: 4px 0 0;
  font-size: var(--fs-caption);
  color: var(--danger);
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
