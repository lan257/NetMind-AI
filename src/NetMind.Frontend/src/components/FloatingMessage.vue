<script setup>
defineProps({
  toast: { type: Object, required: true }
});
</script>

<template>
  <transition name="toast">
    <div v-if="toast.text" class="floating-message" :class="toast.type" data-testid="floating-message">
      {{ toast.text }}
    </div>
  </transition>
</template>
