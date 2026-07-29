import hashlib
import subprocess
import os
import sys

def get_files(ws_path):
    # Get all tracked files
    try:
        files = subprocess.check_output(['git', 'ls-files', ws_path]).decode('utf-8').splitlines()
        return files
    except:
        return []

def process_files(ws_path, exclude_dirs=None, exclude_files=None):
    all_files = get_files(ws_path)
    files = []
    for f in all_files:
        # Simple exclusion logic
        if exclude_dirs and any(ed in f for ed in exclude_dirs):
            continue
        if exclude_files and any(ef in f for ef in exclude_files):
            continue
        files.append(f)
    files.sort()
    
    hasher = hashlib.sha256()
    total_bytes = 0
    count = 0
    
    for f in files:
        # Payload
        try:
            payload = subprocess.check_output(['git', 'show', f':{f}'])
            # Path relative to workspace
            rel_path = os.path.relpath(f, ws_path).replace('\\', '/')
            hasher.update(rel_path.encode('utf-8'))
            hasher.update(payload)
            total_bytes += len(payload)
            count += 1
        except:
            continue
            
    return count, total_bytes, hasher.hexdigest()

# Calculate
# 1. document-extraction
c1, b1, s1 = process_files("workspaces/document-extraction")
print(f"DocExtraction: {c1} files, {b1} bytes, SHA-256 {s1}")

# 2. report-renderer
c2, b2, s2 = process_files("workspaces/report-renderer")
print(f"ReportRenderer: {c2} files, {b2} bytes, SHA-256 {s2}")

# 3. ai-centre
c3, b3, s3 = process_files("workspaces/ai-centre", exclude_dirs=["skills", "ml-ops/data", ".github"], exclude_files=["caches"])
print(f"AiCentre: {c3} files, {b3} bytes, SHA-256 {s3}")

# 4. ai-centre/skills
c4, b4, s4 = process_files("workspaces/ai-centre/skills", exclude_dirs=[".github", "assets/style-examples", "fixtures/style-examples"], exclude_files=["caches"])
print(f"AiSkills: {c4} files, {b4} bytes, SHA-256 {s4}")
