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
  const codeSpans = [];
  const html = escapeHtml(value)
    .replace(/`([^`]+)`/g, (_, code) => {
      const token = `@@NETMINDCODE${codeSpans.length}@@`;
      codeSpans.push(`<code>${code}</code>`);
      return token;
    })
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/~~([^~]+)~~/g, '<del>$1</del>')
    .replace(/--([^-]+)--/g, '<del>$1</del>')
    .replace(/\*([^*]+)\*/g, '<em>$1</em>')
    .replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>')
    .replace(/\[\[([^|\]]+)\|(\d+)\]\]/g, '<a href="#" class="node-ref" data-id="$2">$1</a>');

  return html.replace(/@@NETMINDCODE(\d+)@@/g, (_, index) => codeSpans[Number(index)]);
}

export function renderMarkdown(value) {
  const lines = String(value ?? '').replace(/\r\n/g, '\n').split('\n');
  const html = [];
  let paragraph = [];
  let listItems = [];
  let listStart = 1;
  let listType = '';
  let codeLines = [];
  let quoteLines = [];
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
    const startAttribute = listType === 'ol' && listStart !== 1 ? ` start="${listStart}"` : '';
    html.push(`<${listType}${startAttribute}>${listItems.map((item) => `<li>${renderInline(item)}</li>`).join('')}</${listType}>`);
    listItems = [];
    listStart = 1;
    listType = '';
  };

  const flushCode = () => {
    if (codeLines.length === 0) {
      return;
    }
    html.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
    codeLines = [];
  };

  const flushQuote = () => {
    if (quoteLines.length === 0) {
      return;
    }
    html.push(`<blockquote>${quoteLines.map(renderInline).join('<br>')}</blockquote>`);
    quoteLines = [];
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
        flushQuote();
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
      flushQuote();
      return;
    }

    if (isTableRow(line)) {
      if (!inTable) {
        flushParagraph();
        flushList();
        flushQuote();
        inTable = true;
      }
      tableRows.push(line);
      return;
    }

    if (inTable) {
      flushTable();
      inTable = false;
    }

    const quote = line.match(/^\s*>\s?(.*)$/);
    if (quote) {
      flushParagraph();
      flushList();
      quoteLines.push(quote[1]);
      return;
    }

    const heading = line.match(/^(#{1,3})\s+(.+)$/);
    if (heading) {
      flushParagraph();
      flushList();
      flushQuote();
      html.push(`<h${heading[1].length}>${renderInline(heading[2])}</h${heading[1].length}>`);
      return;
    }

    if (/^\s*-{3,}\s*$/.test(line)) {
      flushParagraph();
      flushList();
      flushQuote();
      html.push('<hr>');
      return;
    }

    const unorderedList = line.match(/^\s*[-*]\s+(.+)$/);
    if (unorderedList) {
      flushParagraph();
      flushQuote();
      if (listType && listType !== 'ul') {
        flushList();
      }
      listType = 'ul';
      listItems.push(unorderedList[1]);
      return;
    }

    const orderedList = line.match(/^\s*(\d+)\.\s+(.+)$/);
    if (orderedList) {
      flushParagraph();
      flushQuote();
      if (listType && listType !== 'ol') {
        flushList();
      }
      if (!listType) {
        listType = 'ol';
        listStart = Number(orderedList[1]);
      }
      listItems.push(orderedList[2]);
      return;
    }

    flushList();
    flushQuote();
    paragraph.push(line);
  });

  if (inCode) {
    flushCode();
  }
  if (inTable) {
    flushTable();
  }
  flushQuote();
  flushParagraph();
  flushList();

  return html.join('');
}
