  from typing import Any
   import os
   def run(params: dict[str, Any]) -> dict[str, Any]:
       filepath = params["filepath"]
       operation = params.get("operation", "replace")
       content = params.get("content", "")
       old_string = params.get("old_string", "")
       # 安全检查：确保路径合法
       if not os.path.exists(filepath):
           raise FileNotFoundError(f"文件 {filepath} 不存在")
       with open(filepath, 'r', encoding='utf-8') as f:
           original = f.read()
       if operation == "replace":
           if not old_string:
               raise ValueError("replace 操作需要提供 old_string")
           new_content = original.replace(old_string, content)
       elif operation == "append":
           new_content = original + content
       elif operation == "write":
           new_content = content
    # 分块写入，避免一次性写入大量文本
    chunk_size = 4000
    with open(filepath, 'w', encoding='utf-8') as f:
        for i in range(0, len(new_content), chunk_size):
            f.write(new_content[i:i+chunk_size])
    return {"status": "success", "filepath": filepath, "operation": operation}
