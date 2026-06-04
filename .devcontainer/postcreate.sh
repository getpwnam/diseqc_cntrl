#!/usr/bin/env bash

set -e


echo "Setting up environment..."

#pip install -r requirements.txt

# Git prompt
if ! grep -q "### devcontainer git prompt ###" ~/.bashrc; then
cat >> ~/.bashrc <<'EOF'
### devcontainer git prompt ###
parse_git_branch() { git branch --show-current 2>/dev/null; }
parse_git_dirty() { [[ -n $(git status --porcelain 2>/dev/null) ]] && printf '*'; }
export PS1='\[\e[1;36m\]\u@\h\[\e[0m\]:\[\e[1;33m\]\w\[\e[0;32m\]$(git rev-parse --is-inside-work-tree >/dev/null 2>&1 && printf " (%s%s)" "$(parse_git_branch)" "$(parse_git_dirty)")\[\e[0m\]\$ '
### end devcontainer git prompt ###

alias nuget="dotnet nuget"


EOF
fi

#git config --global safe.directory '*'

echo "Environment setup complete."