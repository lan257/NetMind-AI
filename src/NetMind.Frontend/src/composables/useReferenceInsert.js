import { ref, watch } from 'vue';

/**
 * Composable for handling [[ reference insertion in textareas.
 * Tracks cursor position and inserts references at the cursor,
 * rather than appending to the end of text.
 */
export function useReferenceInsert(contentRef, searchNodesFn) {
  const showRefDialog = ref(false);
  const searchKeyword = ref('');
  const searchResults = ref([]);
  const searching = ref(false);

  // Store the position where [[ was typed
  const triggerPosition = ref({ start: -1, end: -1 });

  async function handleSearch(query) {
    if (!query) {
      searchResults.value = [];
      return;
    }
    searching.value = true;
    try {
      const results = await searchNodesFn(query);
      searchResults.value = results.filter(n => n.id !== -1);
    } finally {
      searching.value = false;
    }
  }

  function insertReference(node) {
    const refText = `[[${node.title}|${node.id}]]`;
    const content = contentRef.value || '';

    if (triggerPosition.value.start >= 0 && triggerPosition.value.end >= 0) {
      // Insert at trigger position
      const before = content.slice(0, triggerPosition.value.start);
      const after = content.slice(triggerPosition.value.end);
      contentRef.value = before + refText + after;
    } else {
      // Fallback: append to end
      contentRef.value = content + (content.length > 0 && !content.endsWith('\n') ? '\n' : '') + refText;
    }

    showRefDialog.value = false;
    searchKeyword.value = '';
    searchResults.value = [];
    triggerPosition.value = { start: -1, end: -1 };
  }

  // Watch for [[ trigger in text content
  function setupWatch() {
    return watch(contentRef, (newVal, oldVal) => {
      if (!newVal || !oldVal) return;
      // Only trigger when [[ is typed (characters added, not deleted)
      if (newVal.length <= (oldVal?.length ?? 0)) return;

      const diff = newVal.slice(oldVal.length);
      if (diff === '[[') {
        // [[ was typed at the end - find the actual [[ position
        const idx = newVal.lastIndexOf('[[', newVal.length - 2);
        if (idx >= 0) {
          triggerPosition.value = { start: idx, end: newVal.length };
          showRefDialog.value = true;
        }
      }
    });
  }

  // Manual trigger: call this when [[ is detected at any cursor position
  function triggerAtPosition(textareaEl) {
    if (!textareaEl) return;
    const start = textareaEl.selectionStart;
    const end = textareaEl.selectionEnd;
    const text = textareaEl.value || '';

    // Check if the last 2 characters before cursor are [[
    if (start >= 2 && text.slice(start - 2, start) === '[[') {
      triggerPosition.value = { start: start - 2, end: start };
      showRefDialog.value = true;
    }
  }

  function resetDialog() {
    showRefDialog.value = false;
    searchKeyword.value = '';
    searchResults.value = [];
    triggerPosition.value = { start: -1, end: -1 };
  }

  return {
    showRefDialog,
    searchKeyword,
    searchResults,
    searching,
    handleSearch,
    insertReference,
    setupWatch,
    triggerAtPosition,
    resetDialog
  };
}
