function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function isTableRow(line) {
  const t = line.trim();
  return t.startsWith('|') && t.endsWith('|');
}

function parseTableRow(line) {
  return line.trim()
    .replace(/^\||\|$/g, '')
    .split('|')
    .map(c => c.trim());
}

function renderTableCell(cell, tag) {
  return `<${tag}>${renderInline(cell)}</${tag}>`;
}

function renderInline(value) {
  return escapeHtml(value)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\*([^*]+)\*/g, '<em>$1</em>')
    .replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>')
    .replace(/\[\[([^|\]]+)\|(\d+)\]\]/g, '<a href="#" class="node-ref" data-id="$2">$1</a>');
}

export function renderMarkdown(value) {
  const lines = String(value ?? '').replace(/\r\n/g, '\n').split('\n');
  const html = [];
  let paragraph = [];
  let listItems = [];
  let codeLines = [];
  let tableRows = [];
  let inCode = false;
  let inTable = false;

  const flushParagraph = () => {
    if (paragraph.length === 0) {
      return;
    }
    html.push(`<p>${paragraph.map(renderInline).join('<br>')}</p>`);
    paragraph = [];
  };

  const flushList = () => {
    if (listItems.length === 0) {
      return;
    }
    html.push(`<ul>${listItems.map((item) => `<li>${renderInline(item)}</li>`).join('')}</ul>`);
    listItems = [];
  };

  const flushCode = () => {
    if (codeLines.length === 0) {
      return;
    }
    html.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
    codeLines = [];
  };

  const flushTable = () => {
    if (tableRows.length < 2) {
      tableRows.forEach(row => paragraph.push(row));
      tableRows = [];
      flushParagraph();
      return;
    }
    const headers = parseTableRow(tableRows[0]);
    const bodyRows = tableRows.slice(2);
    const thead = `<thead><tr>${headers.map(h => renderTableCell(h, 'th')).join('')}</tr></thead>`;
    const tbody = bodyRows.length > 0
      ? `<tbody>${bodyRows.map(row => {
          const cells = parseTableRow(row);
          return `<tr>${headers.map((_, i) => renderTableCell(cells[i] ?? '', 'td')).join('')}</tr>`;
        }).join('')}</tbody>`
      : '';
    html.push(`<table>${thead}${tbody}</table>`);
    tableRows = [];
  };

  lines.forEach((line) => {
    if (line.trim().startsWith('```')) {
      if (inCode) {
        flushCode();
      } else {
        if (inTable) flushTable();
        inTable = false;
        flushParagraph();
        flushList();
      }
      inCode = !inCode;
      return;
    }

    if (inCode) {
      codeLines.push(line);
      return;
    }

    if (!line.trim()) {
      if (inTable) {
        flushTable();
        inTable = false;
      }
      flushParagraph();
      flushList();
      return;
    }

    if (isTableRow(line)) {
      if (!inTable) {
        flushParagraph();
        flushList();
        inTable = true;
      }
      tableRows.push(line);
      return;
    }

    if (inTable) {
      flushTable();
      inTable = false;
    }

    const heading = line.match(/^(#{1,3})\s+(.+)$/);
    if (heading) {
      flushParagraph();
      flushList();
      html.push(`<h${heading[1].length}>${renderInline(heading[2])}</h${heading[1].length}>`);
      return;
    }

    const list = line.match(/^\s*[-*]\s+(.+)$/);
    if (list) {
      flushParagraph();
      listItems.push(list[1]);
      return;
    }

    flushList();
    paragraph.push(line);
  });

  if (inCode) {
    flushCode();
  }
  if (inTable) {
    flushTable();
  }
  flushParagraph();
  flushList();

  return html.join('');
}