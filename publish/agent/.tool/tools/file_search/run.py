import os
import fnmatch
from typing import Any

def run(params: dict[str, Any]) -> dict[str, Any]:
    search_dir = params.get("search_dir", ".")
    pattern = params.get("pattern", "*")
    recursive = params.get("recursive", True)
    if not os.path.isdir(search_dir):
        return {"error": f"Directory not found: {search_dir}", "success": False}
    results = []
    if recursive:
        for root, dirs, files in os.walk(search_dir):
            for f in files:
                if fnmatch.fnmatch(f, pattern):
                    results.append(os.path.join(root, f))
    else:
        for f in os.listdir(search_dir):
            if os.path.isfile(os.path.join(search_dir, f)) and fnmatch.fnmatch(f, pattern):
                results.append(os.path.join(search_dir, f))
    return {"files": results, "count": len(results), "success": True}
