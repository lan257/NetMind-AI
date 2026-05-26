from typing import Any

def run(params: dict[str, Any]) -> dict[str, Any]:
    filepath = params.get("filepath")
    start_line = params.get("start_line")
    end_line = params.get("end_line")
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        total_lines = len(lines)
        if start_line is None and end_line is None:
            content = ''.join(lines)
            return {"success": True, "content": content, "total_lines": total_lines}
        else:
            s = start_line - 1 if start_line else 0
            e = end_line if end_line else total_lines
            selected = lines[s:e]
            return {"success": True, "content": ''.join(selected), "total_lines": total_lines, "start_line": start_line, "end_line": end_line}
    except Exception as e:
        return {"success": False, "error": str(e)}