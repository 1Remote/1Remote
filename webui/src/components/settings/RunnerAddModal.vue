<script setup>
/**
 * 添加运行器模态（自 RunnerGroup.vue 拆出）：对齐 WPF CmdAddRunner 的 InputBox 流程——
 * 只输入名称（非空 + 协议内唯一），确认后由父建默认值运行器进列表（V4 round16 定案：
 * 不写默认运行器，仅滚动入视野 + 3 秒高亮定位）；exe 路径/启动参数等在卡片内继续配置。
 * 名称草稿与校验归本组件（打开即重置，上次未提交的草稿不带入新会话）；确认经
 * save 事件上抛 trimmed 名称（按钮 disabled 与回车守卫同源：校验未过不动作）。
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDialog, useMessage } from 'naive-ui'
import { useDraftGuard } from '../../composables/useDraftGuard'
import { useRequiredGuard } from '../../composables/useRequiredGuard'
import { onFormEnter } from '../../utils/formEnter'

const props = defineProps({
  /** 模态显隐（v-model:show） */
  show: { type: Boolean, default: false },
  /** 当前协议内已有的 runner 名单（唯一性校验域） */
  existingNames: { type: Array, default: () => [] },
})
const emit = defineEmits(['update:show', 'save'])
const { t } = useI18n()
const message = useMessage()
const dialog = useDialog()

const name = ref('')

// 打开即重置：上次未提交的草稿不带入新会话
watch(
  () => props.show,
  (v) => {
    if (v) {
      name.value = ''
      touched.value = false // round13 N19：重开复位 touched，空表单不再闪「名称必填」
    }
  }
)

// 名称校验 = WPF CmdAddRunner Runner弹窗的 InputBox 规则：非空 + 协议内唯一。
// touched 门控（round8 P5）：红框/红字是"真实违规"信号，打开瞬间不预报错——
// 用户未碰过输入框时不显示（空值只禁保存钮），与全库「红星预告要求 + 红字留给真实违规」惯例一致
const touched = ref(false)
const nameError = computed(() => {
  if (!touched.value) return ''
  const n = name.value.trim()
  if (!n) return t('settings.r.nameRequired')
  if (props.existingNames.includes(n)) return t('settings.r.nameExists', { name: n })
  return ''
})
const valid = computed(() => {
  const n = name.value.trim()
  return !!n && !props.existingNames.includes(n)
})

function save() {
  if (!valid.value) return
  emit('save', name.value.trim())
}

// 必填项统一守卫（round14 需求）：名称未填=假禁用+提示+强点聚焦；重名（红字已提示）真禁
const reqGuard = useRequiredGuard({ getMissing: () => (name.value.trim() ? [] : ['name']) })

// 草稿守卫（round13 统一关闭语义）：名称空=干净（遮罩即关），有输入走提示/确认
const guard = useDraftGuard({
  isDirty: () => name.value.trim() !== '',
  doClose: () => {
    emit('update:show', false)
  },
  dialog,
  message,
  t,
})

// 模态表单回车=保存：共通语义见 utils/formEnter.js（与保存按钮同守卫：名称校验未过不动作）
</script>

<template>
  <n-modal
    :show="show"
    preset="card"
    :title="t('settings.r.addTitle')"
    :bordered="false"
    :style="{ width: 'min(480px, 92vw)' }"
    role="dialog"
    aria-modal="true"
    :mask-closable="false"
    @update:show="guard.requestClose"
    @mask-click="guard.onMaskClick"
  >
    <div class="form" @keydown="onFormEnter($event, () => reqGuard.trySave(save))">
      <div class="s-form-row" :class="{ 'req-hot': reqGuard.isHot('name') }" data-req-key="name">
        <label>{{ t('editor.f.Name') }}<span class="req-star">*</span></label>
        <div>
          <n-input
            size="small"
            v-model:value="name"
            :status="nameError ? 'error' : reqGuard.isHot('name') ? 'error' : undefined"
            :input-props="{ spellcheck: false }"
            @input="touched = true"
          />
          <p v-if="nameError" class="f-err">{{ nameError }}</p>
        </div>
      </div>
    </div>
    <template #footer>
      <div class="modal-actions">
        <n-button size="small" @click="guard.requestClose">{{ t('editor.cancel') }}</n-button>
        <!-- round14 统一必填处理：未填=假禁用+旁侧提示，强点聚焦名称框；重名真禁 -->
        <span v-if="reqGuard.blocked" class="req-hint">{{ t('modal.requiredHint') }}</span>
        <n-button
          size="small"
          type="primary"
          ghost
          :class="{ 'req-blocked': reqGuard.blocked }"
          :disabled="!!nameError && !reqGuard.blocked"
          @click="reqGuard.trySave(save)"
        >
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
