---
# Tasks to manage filesystem mounts, sizes, and removal of unlisted filesystems
- name: Ensure the filesystems are correctly managed
  block:

    - name: Gather mounted filesystems
      ansible.builtin.setup:
        gather_subset:
          - "mounts"

    - name: Define list of managed paths from dictionary
      set_fact:
        managed_paths: "{{ nvs_linux_core_filesystem_mounts | dict2items | map(attribute='value.path') | list }}"

    - name: Remove unlisted filesystems
      ansible.builtin.mount:
        path: "{{ item.mount }}"
        src: "{{ item.device }}"
        fstype: "{{ item.fstype }}"
        state: absent
      loop: "{{ ansible_mounts }}"
      when: item.mount not in managed_paths

    - name: Create or resize logical volume
      community.general.lvol:
        vg: "{{ item.value.src | regex_replace('/dev/(.*)/.*', '\\1') }}"
        lv: "{{ item.value.src | regex_replace('/dev/.*/(.*)', '\\1') }}"
        size: "{{ item.value.size }}"
        resizefs: yes
        state: present
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"

    - name: Create mount point
      ansible.builtin.file:
        path: "{{ item.value.path }}"
        state: directory
        owner: root
        group: root
        mode: '0755'
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"

    - name: Mount the filesystem
      ansible.builtin.mount:
        path: "{{ item.value.path }}"
        src: "{{ item.value.src }}"
        fstype: ext4  # Assuming ext4, adjust as necessary
        opts: defaults
        state: mounted
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      notify: Mount changes
  when: nvs_linux_core_filesystem_mounts is defined
